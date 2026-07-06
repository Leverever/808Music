from dataclasses import dataclass
from typing import Any


def _read(message: dict[str, Any], *names: str) -> Any:
    for name in names:
        if name in message:
            return message[name]

    raise KeyError(f"Missing required message field: {names[0]}")


@dataclass(frozen=True)
class AudioAnalysisJob:
    analysis_id: str
    track_id: int
    master_object_key: str
    provider_name: str
    model_name: str
    model_version: str

    @staticmethod
    def from_message(message: dict[str, Any]) -> "AudioAnalysisJob":
        return AudioAnalysisJob(
            analysis_id=str(_read(message, "AnalysisId", "analysisId")),
            track_id=int(_read(message, "TrackId", "trackId")),
            master_object_key=str(_read(message, "MasterObjectKey", "masterObjectKey")),
            provider_name=str(_read(message, "ProviderName", "providerName")),
            model_name=str(_read(message, "ModelName", "modelName")),
            model_version=str(_read(message, "ModelVersion", "modelVersion")),
        )
