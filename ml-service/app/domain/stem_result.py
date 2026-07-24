from dataclasses import asdict, dataclass
from pathlib import Path


@dataclass(frozen=True)
class SeparatedStem:
    stem_type: str
    path: Path
    content_type: str
    codec: str | None
    size_bytes: int
    duration_ms: int | None
    sample_rate: int | None
    bitrate_kbps: int | None
    channels: int | None
    checksum_sha256: str | None


@dataclass(frozen=True)
class CompletedStem:
    stem_type: str
    object_key: str
    content_type: str
    size_bytes: int
    duration_ms: int | None
    sample_rate: int | None
    bitrate_kbps: int | None
    codec: str | None
    channels: int | None
    checksum_sha256: str | None

    def to_api_payload(self) -> dict[str, object | None]:
        return asdict(self)
