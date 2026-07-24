from pathlib import Path
import json
import logging
import urllib.request

import numpy as np
from essentia.standard import MonoLoader, TensorflowPredictEffnetDiscogs, TensorflowPredict2D

from app.domain import (
    AudioAnalysisResult,
    AudioAnalysisTag,
    ClassificationHeadSpec,
    TaggingPolicy,
    load_custom_head_specs,
    parse_discogs_class,
    select_ranked_labels,
)

logger = logging.getLogger(__name__)


class EssentiaMtgJamendoAnalyzer:
    _EMBEDDING_MODEL_NAME = "discogs-effnet-bs64-1"
    _EMBEDDING_OUTPUT = "PartitionedCall:1"
    _STYLE_OUTPUT = "PartitionedCall:0"
    _SAMPLE_RATE = 16000
    _BASE_URL = "https://essentia.upf.edu/models"

    _DEFAULT_HEADS = (
        ClassificationHeadSpec(
            namespace="top50tags",
            model_name="mtg_jamendo_top50tags-discogs-effnet-1",
        ),
        ClassificationHeadSpec(
            namespace="genre",
            model_name="mtg_jamendo_genre-discogs-effnet-1",
        ),
        ClassificationHeadSpec(
            namespace="moodtheme",
            model_name="mtg_jamendo_moodtheme-discogs-effnet-1",
        ),
    )
    _EXCLUDED_DISCOGS_CATEGORIES = frozenset({"Non-Music"})

    _MODEL_URLS = {
        "discogs-effnet-bs64-1.pb": (
            f"{_BASE_URL}/feature-extractors/discogs-effnet/discogs-effnet-bs64-1.pb"
        ),
        "discogs-effnet-bs64-1.json": (
            f"{_BASE_URL}/feature-extractors/discogs-effnet/discogs-effnet-bs64-1.json"
        ),
        "mtg_jamendo_top50tags-discogs-effnet-1.pb": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_top50tags/"
            "mtg_jamendo_top50tags-discogs-effnet-1.pb"
        ),
        "mtg_jamendo_top50tags-discogs-effnet-1.json": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_top50tags/"
            "mtg_jamendo_top50tags-discogs-effnet-1.json"
        ),
        "mtg_jamendo_genre-discogs-effnet-1.pb": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_genre/"
            "mtg_jamendo_genre-discogs-effnet-1.pb"
        ),
        "mtg_jamendo_genre-discogs-effnet-1.json": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_genre/"
            "mtg_jamendo_genre-discogs-effnet-1.json"
        ),
        "mtg_jamendo_moodtheme-discogs-effnet-1.pb": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_moodtheme/"
            "mtg_jamendo_moodtheme-discogs-effnet-1.pb"
        ),
        "mtg_jamendo_moodtheme-discogs-effnet-1.json": (
            f"{_BASE_URL}/classification-heads/mtg_jamendo_moodtheme/"
            "mtg_jamendo_moodtheme-discogs-effnet-1.json"
        ),
    }

    def __init__(
        self,
        model_dir: Path,
        top_k_per_namespace: int = 10,
        min_score: float = 0.10,
        auto_download: bool = True,
        discogs_tags_enabled: bool = True,
        discogs_top_k: int = 8,
        discogs_min_score: float = 0.15,
        custom_head_manifest: Path | None = None,
    ) -> None:
        self._model_dir = model_dir
        self._auto_download = auto_download
        self._default_head_policy = TaggingPolicy(
            top_k=top_k_per_namespace,
            min_score=min_score,
        )
        self._discogs_policy = TaggingPolicy(
            top_k=discogs_top_k,
            min_score=discogs_min_score,
        )

        self._ensure_model_file(self._EMBEDDING_MODEL_NAME, ".pb")
        self._ensure_model_file(self._EMBEDDING_MODEL_NAME, ".json")
        embedding_metadata = self._load_metadata(self._EMBEDDING_MODEL_NAME)
        self._discogs_labels = self._read_classes(
            embedding_metadata,
            self._EMBEDDING_MODEL_NAME,
        )

        self._embedding_model = TensorflowPredictEffnetDiscogs(
            graphFilename=str(model_dir / f"{self._EMBEDDING_MODEL_NAME}.pb"),
            output=self._EMBEDDING_OUTPUT,
        )
        self._style_model = (
            TensorflowPredictEffnetDiscogs(
                graphFilename=str(model_dir / f"{self._EMBEDDING_MODEL_NAME}.pb"),
                output=self._STYLE_OUTPUT,
            )
            if discogs_tags_enabled
            else None
        )

        manifest_path = custom_head_manifest or model_dir / "custom-heads.json"
        head_specs = [
            ClassificationHeadSpec(
                namespace=spec.namespace,
                model_name=spec.model_name,
                policy=self._default_head_policy,
            )
            for spec in self._DEFAULT_HEADS
        ]
        head_specs.extend(load_custom_head_specs(manifest_path))
        self._validate_unique_namespaces(head_specs, manifest_path)
        self._heads = [self._load_head(spec) for spec in head_specs]

    def analyze(self, track_id: int, audio_path: Path) -> AudioAnalysisResult:
        audio = MonoLoader(
            filename=str(audio_path),
            sampleRate=self._SAMPLE_RATE,
            resampleQuality=4,
        )()

        segment_embeddings = np.asarray(self._embedding_model(audio), dtype=np.float32)
        if segment_embeddings.ndim != 2 or segment_embeddings.shape[0] == 0:
            raise ValueError("Essentia returned no embeddings for the audio file.")

        track_embedding = segment_embeddings.mean(axis=0)

        tags: list[AudioAnalysisTag] = []
        if self._style_model is not None:
            style_predictions = np.asarray(self._style_model(audio), dtype=np.float32)
            style_scores = (
                style_predictions
                if style_predictions.ndim == 1
                else style_predictions.mean(axis=0)
            )
            tags.extend(self._discogs_tags(style_scores))

        for head in self._heads:
            predictions = np.asarray(head["model"](segment_embeddings), dtype=np.float32)
            scores = predictions if predictions.ndim == 1 else predictions.mean(axis=0)
            tags.extend(
                self._top_labels(
                    namespace=head["namespace"],
                    model_name=head["model_name"],
                    labels=head["labels"],
                    scores=scores,
                    policy=head["policy"],
                )
            )

        return AudioAnalysisResult(
            track_id=track_id,
            embedding_model=self._EMBEDDING_MODEL_NAME,
            embedding=track_embedding.astype(float).tolist(),
            tags=tags,
        )

    def _load_head(self, spec: ClassificationHeadSpec) -> dict[str, object]:
        model_name = spec.model_name
        self._ensure_model_file(model_name, ".pb")
        self._ensure_model_file(model_name, ".json")

        metadata = self._load_metadata(model_name)
        labels = self._read_classes(metadata, model_name)
        tagging_metadata = metadata.get("tagging")
        if tagging_metadata is not None and not isinstance(tagging_metadata, dict):
            raise ValueError(f"Model tagging metadata must be an object: {model_name}")

        policy = spec.policy.with_overrides(tagging_metadata)
        unknown_threshold_labels = set(policy.thresholds).difference(labels)
        if unknown_threshold_labels:
            raise ValueError(
                f"Model tagging metadata contains thresholds for unknown labels "
                f"({', '.join(sorted(unknown_threshold_labels))}): {model_name}"
            )
        input_name = self._schema_node_name(
            metadata,
            collection="inputs",
            default="model/Placeholder",
        )
        output_name = self._schema_node_name(
            metadata,
            collection="outputs",
            default="model/Sigmoid",
            purpose="predictions",
        )

        return {
            "namespace": spec.namespace,
            "labels": labels,
            "model_name": model_name,
            "policy": policy,
            "model": TensorflowPredict2D(
                graphFilename=str(self._model_dir / f"{model_name}.pb"),
                input=input_name,
                output=output_name,
            ),
        }

    def _top_labels(
        self,
        namespace: str,
        model_name: str,
        labels: list[str],
        scores: np.ndarray,
        policy: TaggingPolicy,
    ) -> list[AudioAnalysisTag]:
        return [
            AudioAnalysisTag(
                namespace=namespace,
                label=ranked.label,
                score=ranked.score,
                model_name=model_name,
            )
            for ranked in select_ranked_labels(labels, scores, policy)
        ]

    def _discogs_tags(self, scores: np.ndarray) -> list[AudioAnalysisTag]:
        if len(self._discogs_labels) != len(scores):
            raise ValueError(
                f"Discogs classifier returned {len(scores)} scores for "
                f"{len(self._discogs_labels)} labels."
            )

        eligible_labels: list[str] = []
        eligible_scores: list[float] = []
        parsed_classes = {}

        for raw_label, score in zip(self._discogs_labels, scores):
            parsed = parse_discogs_class(raw_label)
            if parsed.category in self._EXCLUDED_DISCOGS_CATEGORIES:
                continue
            eligible_labels.append(raw_label)
            eligible_scores.append(float(score))
            parsed_classes[raw_label] = parsed

        ranked_labels = select_ranked_labels(
            eligible_labels,
            eligible_scores,
            self._discogs_policy,
        )
        return [
            AudioAnalysisTag(
                namespace=parsed_classes[ranked.label].namespace,
                label=parsed_classes[ranked.label].label,
                score=ranked.score,
                model_name=self._EMBEDDING_MODEL_NAME,
            )
            for ranked in ranked_labels
        ]

    def _load_metadata(self, model_name: str) -> dict:
        with (self._model_dir / f"{model_name}.json").open("r", encoding="utf-8") as file:
            metadata = json.load(file)
        if not isinstance(metadata, dict):
            raise ValueError(f"Model metadata must be a JSON object: {model_name}")
        return metadata

    @staticmethod
    def _read_classes(metadata: dict, model_name: str) -> list[str]:
        labels = metadata.get("classes")
        if not isinstance(labels, list) or not labels or not all(
            isinstance(label, str) and label.strip() for label in labels
        ):
            raise ValueError(f"Model metadata contains no valid classes: {model_name}")
        return labels

    @staticmethod
    def _schema_node_name(
        metadata: dict,
        collection: str,
        default: str,
        purpose: str | None = None,
    ) -> str:
        schema = metadata.get("schema")
        if not isinstance(schema, dict):
            return default
        nodes = schema.get(collection)
        if not isinstance(nodes, list):
            return default

        if purpose is not None:
            for node in nodes:
                if (
                    isinstance(node, dict)
                    and node.get("output_purpose") == purpose
                    and isinstance(node.get("name"), str)
                ):
                    return node["name"]

        for node in nodes:
            if isinstance(node, dict) and isinstance(node.get("name"), str):
                return node["name"]
        return default

    @staticmethod
    def _validate_unique_namespaces(
        specs: list[ClassificationHeadSpec],
        manifest_path: Path,
    ) -> None:
        namespaces = [spec.namespace for spec in specs]
        if len(namespaces) != len(set(namespaces)):
            raise ValueError(
                "Built-in and custom classification head namespaces must be unique: "
                f"{manifest_path}"
            )

    def _ensure_model_file(self, model_name: str, suffix: str) -> None:
        file_name = f"{model_name}{suffix}"
        path = self._model_dir / file_name
        if not path.exists():
            if self._auto_download:
                self._download_model_file(file_name, path)
                return

            raise FileNotFoundError(
                f"Missing Essentia model file: {path}. "
                "Download the required Essentia models or set ESSENTIA_AUTO_DOWNLOAD_MODELS=true."
            )

    def _download_model_file(self, file_name: str, destination: Path) -> None:
        url = self._MODEL_URLS.get(file_name)
        if url is None:
            raise FileNotFoundError(f"Missing Essentia model file and no download URL is configured: {destination}")

        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary_path = destination.with_suffix(destination.suffix + ".tmp")

        logger.info("Downloading Essentia model file %s", file_name)
        try:
            with urllib.request.urlopen(url, timeout=120) as response:
                with temporary_path.open("wb") as file:
                    while True:
                        chunk = response.read(1024 * 1024)
                        if not chunk:
                            break
                        file.write(chunk)

            temporary_path.replace(destination)
        except Exception as exc:
            temporary_path.unlink(missing_ok=True)
            raise FileNotFoundError(
                f"Could not download Essentia model file {file_name} from {url}. "
                "Either allow the worker internet access or place the file in ESSENTIA_MODEL_DIR."
            ) from exc
