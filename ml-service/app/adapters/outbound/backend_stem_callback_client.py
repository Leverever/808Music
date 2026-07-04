import requests

from app.application.errors import BackendCallbackError
from app.domain import CompletedStem


class BackendStemCallbackClient:
    def __init__(self, base_url: str, internal_api_key: str) -> None:
        self._base_url = base_url.rstrip("/")
        self._headers = {
            "X-Internal-Api-Key": internal_api_key,
            "Content-Type": "application/json",
        }

    def mark_processing(self, stem_set_id: str) -> None:
        self._post(f"/api/internal/stem-separation/{stem_set_id}/processing")

    def mark_complete(self, stem_set_id: str, stems: list[CompletedStem]) -> None:
        self._post(
            f"/api/internal/stem-separation/{stem_set_id}/complete",
            {
                "stems": [
                    self._to_camel_case_payload(stem.to_api_payload())
                    for stem in stems
                ],
            },
        )

    def mark_failed(self, stem_set_id: str, error_message: str) -> None:
        self._post(
            f"/api/internal/stem-separation/{stem_set_id}/failed",
            {"errorMessage": error_message[:1000]},
        )

    def _post(self, path: str, payload: dict | None = None) -> None:
        response = requests.post(
            f"{self._base_url}{path}",
            json=payload,
            headers=self._headers,
            timeout=30,
        )

        if not response.ok:
            raise BackendCallbackError(
                response.status_code,
                response.text,
            )

    @staticmethod
    def _to_camel_case_payload(payload: dict[str, object | None]) -> dict[str, object | None]:
        return {
            "stemType": payload["stem_type"],
            "objectKey": payload["object_key"],
            "contentType": payload["content_type"],
            "sizeBytes": payload["size_bytes"],
            "durationMs": payload["duration_ms"],
            "sampleRate": payload["sample_rate"],
            "bitrateKbps": payload["bitrate_kbps"],
            "codec": payload["codec"],
            "channels": payload["channels"],
            "checksumSha256": payload["checksum_sha256"],
        }
