from dataclasses import dataclass
from typing import Any


@dataclass(frozen=True)
class ClusterableTrackTag:
    namespace: str
    label: str
    score: float


@dataclass(frozen=True)
class ClusterableTrack:
    track_id: int
    embedding: list[float]
    tags: list[ClusterableTrackTag]


@dataclass(frozen=True)
class ClusterAssignment:
    track_id: int
    cluster_key: str
    is_noise: bool
    distance_to_center: float | None
    membership_score: float | None


@dataclass(frozen=True)
class ClusterSummary:
    cluster_key: str
    name: str
    size: int
    top_tags: list[ClusterableTrackTag]


@dataclass(frozen=True)
class ClusteringResult:
    cluster_run_id: str
    algorithm_name: str
    embedding_source: str
    assignments: list[ClusterAssignment]
    clusters: list[ClusterSummary]

    def to_api_payload(self) -> dict[str, Any]:
        return {
            "algorithmName": self.algorithm_name,
            "embeddingSource": self.embedding_source,
            "assignments": [
                {
                    "trackId": assignment.track_id,
                    "clusterKey": assignment.cluster_key,
                    "isNoise": assignment.is_noise,
                    "distanceToCenter": assignment.distance_to_center,
                    "membershipScore": assignment.membership_score,
                }
                for assignment in self.assignments
            ],
            "clusters": [
                {
                    "clusterKey": cluster.cluster_key,
                    "name": cluster.name,
                    "size": cluster.size,
                    "topTags": [
                        {
                            "namespace": tag.namespace,
                            "label": tag.label,
                            "score": tag.score,
                        }
                        for tag in cluster.top_tags
                    ],
                }
                for cluster in self.clusters
            ],
        }
