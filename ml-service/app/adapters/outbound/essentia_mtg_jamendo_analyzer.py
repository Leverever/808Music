from pathlib import Path
import json
import logging
import urllib.request

import numpy as np
from essentia.standard import MonoLoader, TensorflowPredictEffnetDiscogs, TensorflowPredict2D

from app.domain import AudioAnalysisResult, AudioAnalysisTag

logger = logging.getLogger(__name__)


class EssentiaMtgJamendoAnalyzer:
    _EMBEDDING_MODEL_NAME = "discogs-effnet-bs64-1"
    _EMBEDDING_OUTPUT = "PartitionedCall:1"
    _SAMPLE_RATE = 16000
    _BASE_URL = "https://essentia.upf.edu/models"

    _HEADS = {
        "top50tags": "mtg_jamendo_top50tags-discogs-effnet-1",
        "genre": "mtg_jamendo_genre-discogs-effnet-1",
        "moodtheme": "mtg_jamendo_moodtheme-discogs-effnet-1",
    }

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
    ) -> None:
        self._model_dir = model_dir
        self._top_k_per_namespace = top_k_per_namespace
        self._min_score = min_score
        self._auto_download = auto_download

        self._ensure_model_file(self._EMBEDDING_MODEL_NAME, ".pb")
        self._ensure_model_file(self._EMBEDDING_MODEL_NAME, ".json")

        self._embedding_model = TensorflowPredictEffnetDiscogs(
            graphFilename=str(model_dir / f"{self._EMBEDDING_MODEL_NAME}.pb"),
            output=self._EMBEDDING_OUTPUT,
        )

        self._heads = {
            namespace: self._load_head(model_name)
            for namespace, model_name in self._HEADS.items()
        }

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
        for namespace, head in self._heads.items():
            predictions = np.asarray(head["model"](segment_embeddings), dtype=np.float32)
            scores = predictions if predictions.ndim == 1 else predictions.mean(axis=0)
            tags.extend(
                self._top_labels(
                    namespace=namespace,
                    model_name=head["model_name"],
                    labels=head["labels"],
                    scores=scores,
                )
            )

        return AudioAnalysisResult(
            track_id=track_id,
            embedding_model=self._EMBEDDING_MODEL_NAME,
            embedding=track_embedding.astype(float).tolist(),
            tags=tags,
        )

    def _load_head(self, model_name: str) -> dict[str, object]:
        self._ensure_model_file(model_name, ".pb")
        self._ensure_model_file(model_name, ".json")

        with (self._model_dir / f"{model_name}.json").open("r", encoding="utf-8") as file:
            labels = json.load(file)["classes"]

        return {
            "labels": labels,
            "model_name": model_name,
            "model": TensorflowPredict2D(graphFilename=str(self._model_dir / f"{model_name}.pb")),
        }

    def _top_labels(
        self,
        namespace: str,
        model_name: str,
        labels: list[str],
        scores: np.ndarray,
    ) -> list[AudioAnalysisTag]:
        indexes = np.argsort(scores)[::-1]
        tags: list[AudioAnalysisTag] = []

        for index in indexes:
            score = float(scores[index])
            if score < self._min_score:
                continue

            tags.append(
                AudioAnalysisTag(
                    namespace=namespace,
                    label=labels[index],
                    score=score,
                    model_name=model_name,
                )
            )

            if len(tags) >= self._top_k_per_namespace:
                break

        return tags

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
