from dataclasses import dataclass, field
from typing import Any


def _read(message: dict[str, Any], *names: str) -> Any:
    for name in names:
        if name in message:
            return message[name]

    raise KeyError(f"Missing required message field: {names[0]}")


@dataclass(frozen=True)
class ClusteringJob:
    cluster_run_id: str
    algorithm_name: str
    embedding_source: str
    parameters: dict[str, Any] = field(default_factory=dict)

    @staticmethod
    def from_message(message: dict[str, Any]) -> "ClusteringJob":
        parameters = message.get("Parameters", message.get("parameters", {}))
        if parameters is None:
            parameters = {}

        return ClusteringJob(
            cluster_run_id=str(_read(message, "ClusterRunId", "clusterRunId")),
            algorithm_name=str(_read(message, "AlgorithmName", "algorithmName")),
            embedding_source=str(_read(message, "EmbeddingSource", "embeddingSource")),
            parameters=dict(parameters),
        )
