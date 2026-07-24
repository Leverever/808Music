from contextlib import contextmanager
from pathlib import Path
import shutil


class TemporaryWorkspace:
    def __init__(self, root: Path) -> None:
        self._root = root

    @contextmanager
    def create(self, job_id: str):
        workdir = self._root / job_id
        if workdir.exists():
            shutil.rmtree(workdir)

        workdir.mkdir(parents=True, exist_ok=True)

        try:
            yield workdir
        finally:
            shutil.rmtree(workdir, ignore_errors=True)
