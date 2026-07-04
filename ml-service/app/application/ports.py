from pathlib import Path
from typing import Protocol

from app.domain import CompletedStem, SeparatedStem, StemSeparationJob


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


class BackendStemCallbackPort(Protocol):
    def mark_processing(self, stem_set_id: str) -> None:
        ...

    def mark_complete(self, stem_set_id: str, stems: list[CompletedStem]) -> None:
        ...

    def mark_failed(self, stem_set_id: str, error_message: str) -> None:
        ...


class WorkspacePort(Protocol):
    def create(self, job_id: str):
        ...
