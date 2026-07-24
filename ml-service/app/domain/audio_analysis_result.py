from dataclasses import asdict, dataclass


@dataclass(frozen=True)
class AudioAnalysisTag:
    namespace: str
    label: str
    score: float
    model_name: str

    def to_api_payload(self) -> dict[str, object]:
        return asdict(self)


@dataclass(frozen=True)
class AudioAnalysisResult:
    track_id: int
    embedding_model: str
    embedding: list[float]
    tags: list[AudioAnalysisTag]

    def to_api_payload(self) -> dict[str, object]:
        return {
            "trackId": self.track_id,
            "embeddingModel": self.embedding_model,
            "embedding": self.embedding,
            "tags": [
                {
                    "namespace": tag.namespace,
                    "label": tag.label,
                    "score": tag.score,
                    "modelName": tag.model_name,
                }
                for tag in self.tags
            ],
        }
