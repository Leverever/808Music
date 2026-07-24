from pathlib import Path
import logging

from app.application.errors import BackendCallbackError
from app.application.ports import (
    AudioAnalyzerPort,
    BackendAudioAnalysisCallbackPort,
    ObjectStoragePort,
    WorkspacePort,
)
from app.domain import AudioAnalysisJob

logger = logging.getLogger(__name__)


class AudioAnalysisPipeline:
    def __init__(
        self,
        storage: ObjectStoragePort,
        analyzer: AudioAnalyzerPort,
        backend_client: BackendAudioAnalysisCallbackPort,
        workspace: WorkspacePort,
    ) -> None:
        self._storage = storage
        self._analyzer = analyzer
        self._backend_client = backend_client
        self._workspace = workspace

    def run(self, job: AudioAnalysisJob) -> None:
        logger.info("Starting audio analysis job analysis_id=%s track_id=%s", job.analysis_id, job.track_id)

        try:
            self._backend_client.mark_processing(job.analysis_id)

            with self._workspace.create(job.analysis_id) as workdir:
                master_path = self._master_path(workdir, job.master_object_key)
                self._storage.download(job.master_object_key, master_path)

                result = self._analyzer.analyze(job.track_id, master_path)

                self._backend_client.mark_complete(job.analysis_id, result)
                logger.info("Completed audio analysis job analysis_id=%s", job.analysis_id)
        except BackendCallbackError as exc:
            logger.exception("Backend callback failed analysis_id=%s", job.analysis_id)
            if exc.is_retryable:
                raise

            try:
                self._backend_client.mark_failed(
                    job.analysis_id,
                    f"Backend rejected audio analysis completion: {exc.response_text}",
                )
            except BackendCallbackError:
                logger.exception("Failed to report non-retryable audio analysis callback error")
                raise
        except Exception as exc:
            logger.exception("Audio analysis job failed analysis_id=%s", job.analysis_id)
            try:
                self._backend_client.mark_failed(job.analysis_id, str(exc))
            except BackendCallbackError:
                logger.exception("Failed to report audio analysis failure")
                raise

    @staticmethod
    def _master_path(workdir: Path, object_key: str) -> Path:
        suffix = Path(object_key).suffix or ".audio"
        return workdir / f"master{suffix}"
