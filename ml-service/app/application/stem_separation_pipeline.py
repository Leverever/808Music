from pathlib import Path
import logging

from app.application.errors import BackendCallbackError
from app.application.ports import (
    BackendStemCallbackPort,
    ObjectStoragePort,
    WorkspacePort,
)
from app.application.provider_registry import StemSeparatorRegistry
from app.domain import CompletedStem, StemSeparationJob

logger = logging.getLogger(__name__)


class StemSeparationPipeline:
    def __init__(
        self,
        storage: ObjectStoragePort,
        separator_registry: StemSeparatorRegistry,
        backend_client: BackendStemCallbackPort,
        workspace: WorkspacePort,
    ) -> None:
        self._storage = storage
        self._separator_registry = separator_registry
        self._backend_client = backend_client
        self._workspace = workspace

    def run(self, job: StemSeparationJob) -> None:
        logger.info("Starting stem separation job stem_set_id=%s track_id=%s", job.stem_set_id, job.track_id)

        try:
            self._backend_client.mark_processing(job.stem_set_id)

            with self._workspace.create(job.stem_set_id) as workdir:
                master_path = self._master_path(workdir, job.master_object_key)
                output_dir = workdir / "stems"
                output_dir.mkdir(parents=True, exist_ok=True)

                self._storage.download(job.master_object_key, master_path)

                separator = self._separator_registry.get(job.provider_name)
                separated_stems = separator.separate(job, master_path, output_dir)

                completed_stems: list[CompletedStem] = []
                for stem in separated_stems:
                    object_key = self._stem_object_key(job, stem.stem_type, stem.path)
                    self._storage.upload(stem.path, object_key, stem.content_type)

                    completed_stems.append(
                        CompletedStem(
                            stem_type=stem.stem_type,
                            object_key=object_key,
                            content_type=stem.content_type,
                            size_bytes=stem.size_bytes,
                            duration_ms=stem.duration_ms,
                            sample_rate=stem.sample_rate,
                            bitrate_kbps=stem.bitrate_kbps,
                            codec=stem.codec,
                            channels=stem.channels,
                            checksum_sha256=stem.checksum_sha256,
                        )
                    )

                self._backend_client.mark_complete(job.stem_set_id, completed_stems)
                logger.info("Completed stem separation job stem_set_id=%s", job.stem_set_id)
        except BackendCallbackError as exc:
            logger.exception("Backend callback failed stem_set_id=%s", job.stem_set_id)
            if exc.is_retryable:
                raise

            try:
                self._backend_client.mark_failed(
                    job.stem_set_id,
                    f"Backend rejected stem completion: {exc.response_text}",
                )
            except BackendCallbackError:
                logger.exception("Failed to report non-retryable backend callback error")
                raise
        except Exception as exc:
            logger.exception("Stem separation job failed stem_set_id=%s", job.stem_set_id)
            try:
                self._backend_client.mark_failed(job.stem_set_id, str(exc))
            except BackendCallbackError:
                logger.exception("Failed to report stem separation failure")
                raise

    @staticmethod
    def _master_path(workdir: Path, object_key: str) -> Path:
        suffix = Path(object_key).suffix or ".audio"
        return workdir / f"master{suffix}"

    @staticmethod
    def _stem_object_key(job: StemSeparationJob, stem_type: str, local_path: Path) -> str:
        suffix = local_path.suffix or ".wav"
        normalized_stem = stem_type.strip().lower()
        return f"stems/{job.track_id}/{job.stem_set_id}/{normalized_stem}{suffix}"
