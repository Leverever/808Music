import requests

from app.application.errors import BackendCallbackError
from app.domain import AudioAnalysisResult


class BackendAudioAnalysisCallbackClient:
    def __init__(self, base_url: str, internal_api_key: str) -> None:
        self._base_url = base_url.rstrip("/")
        self._headers = {
            "X-Internal-Api-Key": internal_api_key,
            "Content-Type": "application/json",
        }

    def mark_processing(self, analysis_id: str) -> None:
        self._post(f"/api/internal/audio-analysis/{analysis_id}/processing")

    def mark_complete(self, analysis_id: str, result: AudioAnalysisResult) -> None:
        self._post(
            f"/api/internal/audio-analysis/{analysis_id}/complete",
            result.to_api_payload(),
        )

    def mark_failed(self, analysis_id: str, error_message: str) -> None:
        self._post(
            f"/api/internal/audio-analysis/{analysis_id}/failed",
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
