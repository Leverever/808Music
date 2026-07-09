from collections import defaultdict
from typing import Any

import numpy as np
from sklearn.cluster import AgglomerativeClustering, KMeans
from sklearn.preprocessing import normalize

try:
    from sklearn.cluster import HDBSCAN
except ImportError:  # pragma: no cover - depends on sklearn version
    HDBSCAN = None

from app.domain import (
    ClusterableTrack,
    ClusterableTrackTag,
    ClusterAssignment,
    ClusterSummary,
    ClusteringResult,
)


class KMeansClusteringAlgorithm:
    def cluster(
        self,
        cluster_run_id: str,
        embedding_source: str,
        tracks: list[ClusterableTrack],
        parameters: dict[str, object],
    ) -> ClusteringResult:
        track_ids, x = _prepare_embeddings(tracks)
        n_clusters = _bounded_cluster_count(
            _read_int(parameters, "n_clusters", "nClusters", default=12),
            len(tracks),
        )
        random_state = _read_int(parameters, "random_state", "randomState", default=42)

        if len(tracks) == 1:
            assignments = [_single_assignment(track_ids[0])]
            return _result(cluster_run_id, "kmeans", embedding_source, tracks, assignments)

        model = KMeans(n_clusters=n_clusters, random_state=random_state, n_init="auto")
        labels = model.fit_predict(x)
        centers = model.cluster_centers_

        assignments = []
        for index, track_id in enumerate(track_ids):
            label = int(labels[index])
            distance = float(np.linalg.norm(x[index] - centers[label]))
            assignments.append(
                ClusterAssignment(
                    track_id=track_id,
                    cluster_key=str(label),
                    is_noise=False,
                    distance_to_center=distance,
                    membership_score=_distance_to_membership(distance),
                )
            )

        return _result(cluster_run_id, "kmeans", embedding_source, tracks, assignments)


class AgglomerativeClusteringAlgorithm:
    def cluster(
        self,
        cluster_run_id: str,
        embedding_source: str,
        tracks: list[ClusterableTrack],
        parameters: dict[str, object],
    ) -> ClusteringResult:
        track_ids, x = _prepare_embeddings(tracks)
        n_clusters = _bounded_cluster_count(
            _read_int(parameters, "n_clusters", "nClusters", default=12),
            len(tracks),
        )
        linkage = str(parameters.get("linkage", "ward"))

        if len(tracks) == 1:
            assignments = [_single_assignment(track_ids[0])]
            return _result(cluster_run_id, "agglomerative", embedding_source, tracks, assignments)

        model = AgglomerativeClustering(n_clusters=n_clusters, linkage=linkage)
        labels = model.fit_predict(x)
        centers = _centers_for_labels(x, labels)

        assignments = []
        for index, track_id in enumerate(track_ids):
            label = int(labels[index])
            distance = float(np.linalg.norm(x[index] - centers[label]))
            assignments.append(
                ClusterAssignment(
                    track_id=track_id,
                    cluster_key=str(label),
                    is_noise=False,
                    distance_to_center=distance,
                    membership_score=_distance_to_membership(distance),
                )
            )

        return _result(cluster_run_id, "agglomerative", embedding_source, tracks, assignments)


class HdbscanClusteringAlgorithm:
    def cluster(
        self,
        cluster_run_id: str,
        embedding_source: str,
        tracks: list[ClusterableTrack],
        parameters: dict[str, object],
    ) -> ClusteringResult:
        if HDBSCAN is None:
            raise RuntimeError("HDBSCAN requires scikit-learn with sklearn.cluster.HDBSCAN support.")

        track_ids, x = _prepare_embeddings(tracks)
        if len(tracks) == 1:
            assignments = [_single_assignment(track_ids[0])]
            return _result(cluster_run_id, "hdbscan", embedding_source, tracks, assignments)

        min_cluster_size = _read_int(parameters, "min_cluster_size", "minClusterSize", default=10)
        min_cluster_size = max(2, min(min_cluster_size, len(tracks)))
        min_samples = _read_optional_int(parameters, "min_samples", "minSamples")

        model = HDBSCAN(
            min_cluster_size=min_cluster_size,
            min_samples=min_samples,
            metric=str(parameters.get("metric", "euclidean")),
        )
        labels = model.fit_predict(x)
        probabilities = getattr(model, "probabilities_", None)
        centers = _centers_for_labels(x, labels)

        assignments = []
        for index, track_id in enumerate(track_ids):
            label = int(labels[index])
            is_noise = label == -1
            distance = None if is_noise else float(np.linalg.norm(x[index] - centers[label]))
            membership = None
            if probabilities is not None:
                membership = float(probabilities[index])
            elif distance is not None:
                membership = _distance_to_membership(distance)

            assignments.append(
                ClusterAssignment(
                    track_id=track_id,
                    cluster_key="noise" if is_noise else str(label),
                    is_noise=is_noise,
                    distance_to_center=distance,
                    membership_score=membership,
                )
            )

        return _result(cluster_run_id, "hdbscan", embedding_source, tracks, assignments)


def _prepare_embeddings(tracks: list[ClusterableTrack]) -> tuple[list[int], np.ndarray]:
    if not tracks:
        raise ValueError("At least one track is required for clustering.")

    dimensions = {len(track.embedding) for track in tracks}
    if len(dimensions) != 1:
        raise ValueError(f"All embeddings must have the same dimension, got: {sorted(dimensions)}")

    track_ids = [track.track_id for track in tracks]
    embeddings = np.asarray([track.embedding for track in tracks], dtype=np.float32)
    return track_ids, normalize(embeddings)


def _result(
    cluster_run_id: str,
    algorithm_name: str,
    embedding_source: str,
    tracks: list[ClusterableTrack],
    assignments: list[ClusterAssignment],
) -> ClusteringResult:
    return ClusteringResult(
        cluster_run_id=cluster_run_id,
        algorithm_name=algorithm_name,
        embedding_source=embedding_source,
        assignments=assignments,
        clusters=_summarize_clusters(tracks, assignments),
    )


def _summarize_clusters(
    tracks: list[ClusterableTrack],
    assignments: list[ClusterAssignment],
) -> list[ClusterSummary]:
    tracks_by_id = {track.track_id: track for track in tracks}
    assignments_by_cluster: dict[str, list[ClusterAssignment]] = defaultdict(list)
    for assignment in assignments:
        assignments_by_cluster[assignment.cluster_key].append(assignment)

    summaries = []
    for cluster_key, cluster_assignments in sorted(assignments_by_cluster.items()):
        if cluster_key == "noise":
            summaries.append(
                ClusterSummary(
                    cluster_key=cluster_key,
                    name="Unclustered Tracks",
                    size=len(cluster_assignments),
                    top_tags=[],
                )
            )
            continue

        top_tags = _dominant_tags(
            tracks_by_id[assignment.track_id]
            for assignment in cluster_assignments
        )
        summaries.append(
            ClusterSummary(
                cluster_key=cluster_key,
                name=_cluster_name(top_tags),
                size=len(cluster_assignments),
                top_tags=top_tags,
            )
        )

    return summaries


def _dominant_tags(tracks) -> list[ClusterableTrackTag]:
    weights: dict[tuple[str, str], float] = defaultdict(float)
    for track in tracks:
        for tag in track.tags:
            weights[(tag.namespace, tag.label)] += tag.score

    ordered = sorted(weights.items(), key=lambda item: item[1], reverse=True)[:5]
    return [
        ClusterableTrackTag(
            namespace=namespace,
            label=label,
            score=float(score),
        )
        for (namespace, label), score in ordered
    ]


def _cluster_name(top_tags: list[ClusterableTrackTag]) -> str:
    if not top_tags:
        return "Audio Cluster"

    labels = []
    for tag in top_tags:
        normalized = tag.label.replace("_", " ").replace("-", " ").strip()
        if normalized and normalized not in labels:
            labels.append(normalized)
        if len(labels) == 3:
            break

    return " ".join(label.title() for label in labels) if labels else "Audio Cluster"


def _centers_for_labels(x: np.ndarray, labels: np.ndarray) -> dict[int, np.ndarray]:
    centers = {}
    for label in set(int(value) for value in labels):
        if label == -1:
            continue
        centers[label] = x[labels == label].mean(axis=0)

    return centers


def _single_assignment(track_id: int) -> ClusterAssignment:
    return ClusterAssignment(
        track_id=track_id,
        cluster_key="0",
        is_noise=False,
        distance_to_center=0.0,
        membership_score=1.0,
    )


def _bounded_cluster_count(value: int, track_count: int) -> int:
    return max(1, min(value, track_count))


def _distance_to_membership(distance: float) -> float:
    return float(1.0 / (1.0 + max(distance, 0.0)))


def _read_int(parameters: dict[str, object], *names: str, default: int) -> int:
    for name in names:
        if name in parameters:
            return int(parameters[name])

    return default


def _read_optional_int(parameters: dict[str, object], *names: str) -> int | None:
    for name in names:
        if name in parameters and parameters[name] is not None:
            return int(parameters[name])

    return None
