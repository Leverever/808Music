from pathlib import Path
from typing import Protocol

from app.domain import (
    AudioAnalysisResult,
    ClusterableTrack,
    ClusteringResult,
    CompletedStem,
    SeparatedStem,
    StemSeparationJob,
)


class ObjectStoragePort(Protocol):
    def download(self, object_key: str, destination: Path) -> None:
        ...

    def upload(self, local_path: Path, object_key: str, content_type: str) -> None:
        ...


class StemSeparatorPort(Protocol):
    def separate(
        self,
        job: StemSeparationJob,
        input_path: Path,
        output_dir: Path,
    ) -> list[SeparatedStem]:
        ...


class AudioAnalyzerPort(Protocol):
    def analyze(self, track_id: int, input_path: Path) -> AudioAnalysisResult:
        ...


class ClusteringAlgorithmPort(Protocol):
    def cluster(
        self,
        cluster_run_id: str,
        embedding_source: str,
        tracks: list[ClusterableTrack],
        parameters: dict[str, object],
    ) -> ClusteringResult:
        ...


class BackendStemCallbackPort(Protocol):
    def mark_processing(self, stem_set_id: str) -> None:
        ...

    def mark_complete(self, stem_set_id: str, stems: list[CompletedStem]) -> None:
        ...

    def mark_failed(self, stem_set_id: str, error_message: str) -> None:
        ...


class BackendAudioAnalysisCallbackPort(Protocol):
    def mark_processing(self, analysis_id: str) -> None:
        ...

    def mark_complete(self, analysis_id: str, result: AudioAnalysisResult) -> None:
        ...

    def mark_failed(self, analysis_id: str, error_message: str) -> None:
        ...


class BackendAudioClusteringPort(Protocol):
    def mark_processing(self, cluster_run_id: str) -> None:
        ...

    def fetch_tracks(
        self,
        cluster_run_id: str,
        embedding_source: str,
    ) -> list[ClusterableTrack]:
        ...

    def mark_complete(self, cluster_run_id: str, result: ClusteringResult) -> None:
        ...

    def mark_failed(self, cluster_run_id: str, error_message: str) -> None:
        ...


class WorkspacePort(Protocol):
    def create(self, job_id: str):
        ...
