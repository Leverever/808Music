from __future__ import annotations

import argparse
import csv
from dataclasses import dataclass
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import random
import re
from typing import Callable

import numpy as np
from sklearn.metrics import (
    average_precision_score,
    f1_score,
    precision_recall_fscore_support,
    roc_auc_score,
)
import tensorflow as tf
from tensorflow.python.framework.convert_to_constants import convert_variables_to_constants_v2


EMBEDDING_DIMENSION = 1280
SAFE_ID = re.compile(r"^[A-Za-z0-9_.-]+$")
VALID_SPLITS = frozenset({"train", "validation", "test"})


@dataclass(frozen=True)
class Taxonomy:
    name: str
    version: int
    namespace: str
    embedding_model: str
    labels: tuple[str, ...]
    parents: dict[str, tuple[str, ...]]

    def expand(self, assigned_labels: set[str]) -> set[str]:
        expanded = set(assigned_labels)
        pending = list(assigned_labels)
        while pending:
            label = pending.pop()
            for parent in self.parents[label]:
                if parent not in expanded:
                    expanded.add(parent)
                    pending.append(parent)
        return expanded


@dataclass(frozen=True)
class TrackRecord:
    track_id: str
    artist_id: str
    labels: frozenset[str]
    split: str


@dataclass(frozen=True)
class SplitData:
    features: np.ndarray
    targets: np.ndarray
    track_ids: np.ndarray


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Train and export an Essentia-compatible modern-genre head."
    )
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--taxonomy", type=Path, required=True)
    parser.add_argument("--embeddings-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument(
        "--model-name",
        default="808music-modern-genres-discogs-effnet-1",
    )
    parser.add_argument("--epochs", type=int, default=80)
    parser.add_argument("--batch-size", type=int, default=128)
    parser.add_argument("--learning-rate", type=float, default=1e-3)
    parser.add_argument("--hidden-units", type=int, default=512)
    parser.add_argument("--dropout", type=float, default=0.30)
    parser.add_argument("--segments-per-track", type=int, default=12)
    parser.add_argument("--top-k", type=int, default=8)
    parser.add_argument("--min-score", type=float, default=0.10)
    parser.add_argument("--seed", type=int, default=808)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    _validate_args(args)
    _set_random_seeds(args.seed)

    taxonomy = _load_taxonomy(args.taxonomy)
    records = _read_records(args.dataset, taxonomy)
    _validate_artist_disjoint(records)
    embedding_manifest = _load_embedding_manifest(
        args.embeddings_dir,
        args.dataset,
        taxonomy,
        len(records),
    )

    split_data = {
        split: _load_split(
            records,
            split,
            taxonomy,
            args.embeddings_dir,
            args.segments_per_track,
        )
        for split in sorted(VALID_SPLITS)
    }

    model = _build_model(
        label_count=len(taxonomy.labels),
        hidden_units=args.hidden_units,
        dropout=args.dropout,
    )
    positive_weights = _positive_class_weights(split_data["train"].targets)
    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=args.learning_rate),
        loss=_weighted_binary_crossentropy(positive_weights),
        metrics=[
            tf.keras.metrics.AUC(
                curve="PR",
                multi_label=True,
                num_labels=len(taxonomy.labels),
                name="pr_auc",
            )
        ],
    )

    callbacks = [
        tf.keras.callbacks.EarlyStopping(
            monitor="val_loss",
            patience=10,
            restore_best_weights=True,
        )
    ]
    model.fit(
        split_data["train"].features,
        split_data["train"].targets,
        validation_data=(
            split_data["validation"].features,
            split_data["validation"].targets,
        ),
        epochs=args.epochs,
        batch_size=args.batch_size,
        callbacks=callbacks,
        shuffle=True,
        verbose=2,
    )

    validation_targets, validation_scores = _track_level_predictions(
        model,
        split_data["validation"],
        args.batch_size,
    )
    thresholds = _calibrate_thresholds(
        taxonomy.labels,
        validation_targets,
        validation_scores,
    )

    test_targets, test_scores = _track_level_predictions(
        model,
        split_data["test"],
        args.batch_size,
    )
    metrics = _evaluate(
        taxonomy.labels,
        test_targets,
        test_scores,
        thresholds,
    )

    args.output_dir.mkdir(parents=True, exist_ok=True)
    input_node, output_node = _export_frozen_graph(
        model,
        args.output_dir / f"{args.model_name}.pb",
    )
    model.save_weights(args.output_dir / f"{args.model_name}.weights.h5")
    _write_artifacts(
        output_dir=args.output_dir,
        model_name=args.model_name,
        taxonomy=taxonomy,
        thresholds=thresholds,
        metrics=metrics,
        input_node=input_node,
        output_node=output_node,
        top_k=args.top_k,
        min_score=args.min_score,
        embedding_model_sha256=embedding_manifest["modelSha256"],
        dataset_sha256=embedding_manifest["datasetSha256"],
        taxonomy_sha256=_sha256(args.taxonomy),
    )

    print(f"Exported {args.model_name} to {args.output_dir}")
    print(f"Test macro PR-AUC: {metrics['macroPrAuc']:.4f}")
    print(f"Test macro F1: {metrics['macroF1']:.4f}")


def _validate_args(args: argparse.Namespace) -> None:
    if args.epochs <= 0 or args.batch_size <= 0:
        raise ValueError("epochs and batch-size must be greater than zero.")
    if args.hidden_units < 0 or args.segments_per_track <= 0:
        raise ValueError(
            "hidden-units must be zero or greater and segments-per-track must be "
            "greater than zero."
        )
    if not 0 <= args.dropout < 1:
        raise ValueError("dropout must be in the range [0, 1).")
    if not 0 <= args.min_score <= 1:
        raise ValueError("min-score must be in the range [0, 1].")
    if args.top_k <= 0:
        raise ValueError("top-k must be greater than zero.")
    if not SAFE_ID.fullmatch(args.model_name):
        raise ValueError("model-name may contain only letters, numbers, '.', '_' and '-'.")


def _set_random_seeds(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    tf.random.set_seed(seed)


def _load_taxonomy(path: Path) -> Taxonomy:
    with path.open("r", encoding="utf-8") as file:
        document = json.load(file)

    raw_labels = document.get("labels")
    if not isinstance(raw_labels, list) or not raw_labels:
        raise ValueError("Taxonomy must contain a non-empty labels array.")

    labels: list[str] = []
    parents: dict[str, tuple[str, ...]] = {}
    for item in raw_labels:
        if not isinstance(item, dict):
            raise ValueError("Every taxonomy label must be an object.")
        label = str(item.get("id", "")).strip()
        if not label or not SAFE_ID.fullmatch(label):
            raise ValueError(f"Invalid taxonomy label: {label!r}")
        if label in parents:
            raise ValueError(f"Duplicate taxonomy label: {label}")
        labels.append(label)
        parents[label] = tuple(str(parent) for parent in item.get("parents", []))

    known = set(labels)
    for label, label_parents in parents.items():
        unknown = set(label_parents).difference(known)
        if unknown:
            raise ValueError(
                f"Taxonomy label {label} has unknown parents: {', '.join(sorted(unknown))}"
            )

    taxonomy = Taxonomy(
        name=str(document.get("name", "")).strip(),
        version=int(document.get("version", 1)),
        namespace=str(document.get("namespace", "")).strip(),
        embedding_model=str(document.get("embeddingModel", "")).strip(),
        labels=tuple(labels),
        parents=parents,
    )
    if not taxonomy.name or not taxonomy.namespace or not taxonomy.embedding_model:
        raise ValueError("Taxonomy name, namespace and embeddingModel are required.")

    for label in taxonomy.labels:
        taxonomy.expand({label})
    return taxonomy


def _read_records(path: Path, taxonomy: Taxonomy) -> list[TrackRecord]:
    with path.open("r", encoding="utf-8-sig", newline="") as file:
        reader = csv.DictReader(file)
        required = {"track_id", "artist_id", "labels", "split"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"Dataset is missing columns: {', '.join(sorted(missing))}")
        rows = list(reader)

    records: list[TrackRecord] = []
    seen_track_ids: set[str] = set()
    known_labels = set(taxonomy.labels)
    for row_number, row in enumerate(rows, start=2):
        track_id = row["track_id"].strip()
        artist_id = row["artist_id"].strip()
        split = row["split"].strip().lower()
        labels = {label.strip() for label in row["labels"].split("|") if label.strip()}

        if not track_id or not SAFE_ID.fullmatch(track_id):
            raise ValueError(f"Invalid track_id on row {row_number}: {track_id!r}")
        if track_id in seen_track_ids:
            raise ValueError(f"Duplicate track_id on row {row_number}: {track_id}")
        if not artist_id:
            raise ValueError(f"artist_id is required on row {row_number}.")
        if split not in VALID_SPLITS:
            raise ValueError(
                f"split must be train, validation or test on row {row_number}."
            )
        unknown = labels.difference(known_labels)
        if unknown:
            raise ValueError(
                f"Unknown labels on row {row_number}: {', '.join(sorted(unknown))}"
            )

        seen_track_ids.add(track_id)
        records.append(
            TrackRecord(
                track_id=track_id,
                artist_id=artist_id,
                labels=frozenset(taxonomy.expand(labels)),
                split=split,
            )
        )

    if not records:
        raise ValueError("Dataset contains no tracks.")
    for split in VALID_SPLITS:
        if not any(record.split == split for record in records):
            raise ValueError(f"Dataset contains no {split} tracks.")
    return records


def _validate_artist_disjoint(records: list[TrackRecord]) -> None:
    splits_by_artist: dict[str, set[str]] = {}
    for record in records:
        splits_by_artist.setdefault(record.artist_id, set()).add(record.split)

    leaked = {
        artist: splits
        for artist, splits in splits_by_artist.items()
        if len(splits) > 1
    }
    if leaked:
        examples = ", ".join(
            f"{artist} ({'/'.join(sorted(splits))})"
            for artist, splits in list(leaked.items())[:10]
        )
        raise ValueError(f"Artists must not cross dataset splits: {examples}")


def _load_split(
    records: list[TrackRecord],
    split: str,
    taxonomy: Taxonomy,
    embeddings_dir: Path,
    segments_per_track: int,
) -> SplitData:
    features: list[np.ndarray] = []
    targets: list[np.ndarray] = []
    track_ids: list[str] = []

    for record in records:
        if record.split != split:
            continue

        path = embeddings_dir / f"{record.track_id}.npy"
        embeddings = np.asarray(np.load(path, allow_pickle=False), dtype=np.float32)
        if (
            embeddings.ndim != 2
            or embeddings.shape[0] == 0
            or embeddings.shape[1] != EMBEDDING_DIMENSION
        ):
            raise ValueError(
                f"Embedding file must have shape [segments, {EMBEDDING_DIMENSION}]: {path}"
            )

        selected = _evenly_sample(embeddings, segments_per_track)
        target = np.asarray(
            [1.0 if label in record.labels else 0.0 for label in taxonomy.labels],
            dtype=np.float32,
        )
        features.append(selected)
        targets.append(np.repeat(target[np.newaxis, :], len(selected), axis=0))
        track_ids.extend([record.track_id] * len(selected))

    return SplitData(
        features=np.concatenate(features, axis=0),
        targets=np.concatenate(targets, axis=0),
        track_ids=np.asarray(track_ids),
    )


def _load_embedding_manifest(
    embeddings_dir: Path,
    dataset_path: Path,
    taxonomy: Taxonomy,
    track_count: int,
) -> dict:
    path = embeddings_dir / "embedding-manifest.json"
    with path.open("r", encoding="utf-8") as file:
        manifest = json.load(file)

    expected = {
        "embeddingModel": taxonomy.embedding_model,
        "embeddingDimension": EMBEDDING_DIMENSION,
        "datasetSha256": _sha256(dataset_path),
        "trackCount": track_count,
    }
    mismatches = [
        f"{key}: expected {value!r}, found {manifest.get(key)!r}"
        for key, value in expected.items()
        if manifest.get(key) != value
    ]
    if mismatches:
        raise ValueError(
            "Embedding manifest does not match this training run: "
            + "; ".join(mismatches)
        )
    model_sha256 = manifest.get("modelSha256")
    if not isinstance(model_sha256, str) or len(model_sha256) != 64:
        raise ValueError("Embedding manifest contains no valid modelSha256.")
    return manifest


def _evenly_sample(embeddings: np.ndarray, limit: int) -> np.ndarray:
    if len(embeddings) <= limit:
        return embeddings
    indexes = np.linspace(0, len(embeddings) - 1, num=limit, dtype=int)
    return embeddings[indexes]


def _build_model(
    label_count: int,
    hidden_units: int,
    dropout: float,
) -> tf.keras.Model:
    inputs = tf.keras.Input(shape=(EMBEDDING_DIMENSION,), name="embedding")
    hidden = inputs
    if hidden_units > 0:
        hidden = tf.keras.layers.Dense(
            hidden_units,
            activation="relu",
            name="hidden",
        )(hidden)
        hidden = tf.keras.layers.Dropout(dropout, name="dropout")(hidden)
    outputs = tf.keras.layers.Dense(
        label_count,
        activation="sigmoid",
        name="probabilities",
    )(hidden)
    return tf.keras.Model(inputs=inputs, outputs=outputs, name="modern_genre_head")


def _positive_class_weights(targets: np.ndarray) -> np.ndarray:
    positives = targets.sum(axis=0)
    negatives = len(targets) - positives
    if np.any(positives == 0):
        missing = np.flatnonzero(positives == 0).tolist()
        raise ValueError(f"Training data has no positive examples for label indexes: {missing}")
    return np.clip(negatives / positives, 1.0, 20.0).astype(np.float32)


def _weighted_binary_crossentropy(
    positive_weights: np.ndarray,
) -> Callable[[tf.Tensor, tf.Tensor], tf.Tensor]:
    weights = tf.constant(positive_weights, dtype=tf.float32)

    def loss(y_true: tf.Tensor, y_pred: tf.Tensor) -> tf.Tensor:
        epsilon = tf.keras.backend.epsilon()
        clipped = tf.clip_by_value(y_pred, epsilon, 1.0 - epsilon)
        values = -(
            weights * y_true * tf.math.log(clipped)
            + (1.0 - y_true) * tf.math.log(1.0 - clipped)
        )
        return tf.reduce_mean(values, axis=-1)

    return loss


def _track_level_predictions(
    model: tf.keras.Model,
    data: SplitData,
    batch_size: int,
) -> tuple[np.ndarray, np.ndarray]:
    segment_scores = model.predict(data.features, batch_size=batch_size, verbose=0)
    targets: list[np.ndarray] = []
    scores: list[np.ndarray] = []

    for track_id in dict.fromkeys(data.track_ids.tolist()):
        indexes = np.flatnonzero(data.track_ids == track_id)
        targets.append(data.targets[indexes[0]])
        scores.append(segment_scores[indexes].mean(axis=0))

    return np.stack(targets), np.stack(scores)


def _calibrate_thresholds(
    labels: tuple[str, ...],
    targets: np.ndarray,
    scores: np.ndarray,
) -> dict[str, float]:
    thresholds: dict[str, float] = {}
    candidates = np.linspace(0.05, 0.95, num=91)

    for index, label in enumerate(labels):
        if len(np.unique(targets[:, index])) < 2:
            thresholds[label] = 0.50
            continue
        best_threshold = max(
            candidates,
            key=lambda threshold: f1_score(
                targets[:, index],
                scores[:, index] >= threshold,
                zero_division=0,
            ),
        )
        thresholds[label] = round(float(best_threshold), 4)

    return thresholds


def _evaluate(
    labels: tuple[str, ...],
    targets: np.ndarray,
    scores: np.ndarray,
    thresholds: dict[str, float],
) -> dict:
    per_label = {}
    pr_aucs: list[float] = []
    f1_scores: list[float] = []

    for index, label in enumerate(labels):
        truth = targets[:, index]
        predictions = scores[:, index] >= thresholds[label]
        precision, recall, label_f1, _ = precision_recall_fscore_support(
            truth,
            predictions,
            average="binary",
            zero_division=0,
        )
        pr_auc = (
            float(average_precision_score(truth, scores[:, index]))
            if np.any(truth == 1)
            else 0.0
        )
        roc_auc = (
            float(roc_auc_score(truth, scores[:, index]))
            if len(np.unique(truth)) == 2
            else None
        )
        pr_aucs.append(pr_auc)
        f1_scores.append(float(label_f1))
        per_label[label] = {
            "support": int(truth.sum()),
            "threshold": thresholds[label],
            "precision": round(float(precision), 6),
            "recall": round(float(recall), 6),
            "f1": round(float(label_f1), 6),
            "prAuc": round(pr_auc, 6),
            "rocAuc": round(roc_auc, 6) if roc_auc is not None else None,
        }

    return {
        "macroPrAuc": float(np.mean(pr_aucs)),
        "macroF1": float(np.mean(f1_scores)),
        "perLabel": per_label,
    }


def _export_frozen_graph(
    model: tf.keras.Model,
    output_path: Path,
) -> tuple[str, str]:
    @tf.function(
        input_signature=[
            tf.TensorSpec(
                shape=[None, EMBEDDING_DIMENSION],
                dtype=tf.float32,
                name="embedding",
            )
        ]
    )
    def serving(embedding: tf.Tensor) -> tf.Tensor:
        return tf.identity(model(embedding, training=False), name="probabilities")

    frozen = convert_variables_to_constants_v2(serving.get_concrete_function())
    graph_def = frozen.graph.as_graph_def()
    with output_path.open("wb") as file:
        file.write(graph_def.SerializeToString())

    input_node = frozen.inputs[0].name.split(":", 1)[0]
    output_node = frozen.outputs[0].name.split(":", 1)[0]
    return input_node, output_node


def _write_artifacts(
    output_dir: Path,
    model_name: str,
    taxonomy: Taxonomy,
    thresholds: dict[str, float],
    metrics: dict,
    input_node: str,
    output_node: str,
    top_k: int,
    min_score: float,
    embedding_model_sha256: str,
    dataset_sha256: str,
    taxonomy_sha256: str,
) -> None:
    metadata = {
        "name": model_name,
        "type": "multi-label classifier head",
        "version": str(taxonomy.version),
        "description": "808Music modern-genre classification from Discogs-EffNet embeddings",
        "release_date": datetime.now(timezone.utc).date().isoformat(),
        "framework": "tensorflow",
        "framework_version": tf.__version__,
        "classes": list(taxonomy.labels),
        "model_types": ["frozen_model"],
        "embedding_model": taxonomy.embedding_model,
        "embedding_model_sha256": embedding_model_sha256,
        "dataset": {
            "name": taxonomy.name,
            "manifest_sha256": dataset_sha256,
            "taxonomy_sha256": taxonomy_sha256,
            "metrics": metrics,
        },
        "tagging": {
            "topK": top_k,
            "minScore": min_score,
            "thresholds": thresholds,
        },
        "schema": {
            "inputs": [
                {
                    "name": input_node,
                    "type": "float",
                    "shape": [EMBEDDING_DIMENSION],
                }
            ],
            "outputs": [
                {
                    "name": output_node,
                    "type": "float",
                    "shape": [len(taxonomy.labels)],
                    "op": "Sigmoid",
                    "output_purpose": "predictions",
                }
            ],
        },
    }
    manifest = {
        "heads": [
            {
                "namespace": taxonomy.namespace,
                "modelName": model_name,
                "enabled": True,
                "topK": top_k,
                "minScore": min_score,
            }
        ]
    }

    _write_json(output_dir / f"{model_name}.json", metadata)
    _write_json(output_dir / "custom-heads.json", manifest)
    _write_json(output_dir / f"{model_name}.metrics.json", metrics)


def _write_json(path: Path, value: dict) -> None:
    with path.open("w", encoding="utf-8") as file:
        json.dump(value, file, indent=2, sort_keys=False)
        file.write("\n")


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        while chunk := file.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    main()
