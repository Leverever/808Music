import json

import requests

from app.application.errors import BackendCallbackError
from app.domain import (
    ClusterableTrack,
    ClusterableTrackTag,
    ClusteringResult,
)


class BackendAudioClusteringClient:
    def __init__(self, base_url: str, internal_api_key: str) -> None:
        self._base_url = base_url.rstrip("/")
        self._headers = {
            "X-Internal-Api-Key": internal_api_key,
            "Content-Type": "application/json",
        }

    def mark_processing(self, cluster_run_id: str) -> None:
        self._post(f"/api/internal/audio-clustering/{cluster_run_id}/processing")

    def fetch_tracks(
        self,
        cluster_run_id: str,
        embedding_source: str,
    ) -> list[ClusterableTrack]:
        response = requests.get(
            f"{self._base_url}/api/internal/audio-clustering/{cluster_run_id}/tracks",
            params={"embeddingSource": embedding_source},
            headers=self._headers,
            timeout=60,
        )

        if not response.ok:
            raise BackendCallbackError(response.status_code, response.text)

        payload = response.json()
        return [
            self._track_from_payload(track)
            for track in payload.get("tracks", [])
        ]

    def mark_complete(self, cluster_run_id: str, result: ClusteringResult) -> None:
        self._post(
            f"/api/internal/audio-clustering/{cluster_run_id}/complete",
            result.to_api_payload(),
        )

    def mark_failed(self, cluster_run_id: str, error_message: str) -> None:
        self._post(
            f"/api/internal/audio-clustering/{cluster_run_id}/failed",
            {"errorMessage": error_message[:1000]},
        )

    def _post(self, path: str, payload: dict | None = None) -> None:
        response = requests.post(
            f"{self._base_url}{path}",
            json=payload,
            headers=self._headers,
            timeout=60,
        )

        if not response.ok:
            raise BackendCallbackError(
                response.status_code,
                response.text,
            )

    @staticmethod
    def _track_from_payload(payload: dict) -> ClusterableTrack:
        embedding = payload.get("embedding")
        if isinstance(embedding, str):
            embedding = json.loads(embedding)

        return ClusterableTrack(
            track_id=int(payload["trackId"]),
            embedding=[float(value) for value in embedding],
            tags=[
                ClusterableTrackTag(
                    namespace=str(tag["namespace"]),
                    label=str(tag["label"]),
                    score=float(tag["score"]),
                )
                for tag in payload.get("tags", [])
            ],
        )
