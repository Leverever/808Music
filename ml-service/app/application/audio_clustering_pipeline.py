import logging

from app.application.clustering_registry import ClusteringAlgorithmRegistry
from app.application.errors import BackendCallbackError
from app.application.ports import BackendAudioClusteringPort
from app.domain import ClusteringJob

logger = logging.getLogger(__name__)


class AudioClusteringPipeline:
    def __init__(
        self,
        backend_client: BackendAudioClusteringPort,
        algorithm_registry: ClusteringAlgorithmRegistry,
    ) -> None:
        self._backend_client = backend_client
        self._algorithm_registry = algorithm_registry

    def run(self, job: ClusteringJob) -> None:
        logger.info(
            "Starting audio clustering job cluster_run_id=%s algorithm=%s embedding_source=%s",
            job.cluster_run_id,
            job.algorithm_name,
            job.embedding_source,
        )

        try:
            self._backend_client.mark_processing(job.cluster_run_id)

            tracks = self._backend_client.fetch_tracks(
                job.cluster_run_id,
                job.embedding_source,
            )

            algorithm = self._algorithm_registry.get(job.algorithm_name)
            result = algorithm.cluster(
                cluster_run_id=job.cluster_run_id,
                embedding_source=job.embedding_source,
                tracks=tracks,
                parameters=job.parameters,
            )

            self._backend_client.mark_complete(job.cluster_run_id, result)
            logger.info("Completed audio clustering job cluster_run_id=%s", job.cluster_run_id)
        except BackendCallbackError as exc:
            logger.exception("Backend callback failed cluster_run_id=%s", job.cluster_run_id)
            if exc.is_retryable:
                raise

            try:
                self._backend_client.mark_failed(
                    job.cluster_run_id,
                    f"Backend rejected clustering result: {exc.response_text}",
                )
            except BackendCallbackError:
                logger.exception("Failed to report non-retryable clustering callback error")
                raise
        except Exception as exc:
            logger.exception("Audio clustering job failed cluster_run_id=%s", job.cluster_run_id)
            try:
                self._backend_client.mark_failed(job.cluster_run_id, str(exc))
            except BackendCallbackError:
                logger.exception("Failed to report audio clustering failure")
                raise
