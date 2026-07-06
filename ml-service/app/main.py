import logging

from app.adapters.inbound.rabbitmq_consumer import RabbitMqConsumer
from app.adapters.outbound.backend_audio_analysis_callback_client import BackendAudioAnalysisCallbackClient
from app.adapters.outbound.backend_stem_callback_client import BackendStemCallbackClient
from app.adapters.outbound.demucs_stem_separator import DemucsStemSeparator
from app.adapters.outbound.essentia_mtg_jamendo_analyzer import EssentiaMtgJamendoAnalyzer
from app.adapters.outbound.s3_object_storage import S3ObjectStorage
from app.application.audio_analysis_pipeline import AudioAnalysisPipeline
from app.application.provider_registry import StemSeparatorRegistry
from app.application.stem_separation_pipeline import StemSeparationPipeline
from app.config import Settings
from app.domain import AudioAnalysisJob
from app.infrastructure.workspace import TemporaryWorkspace


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )

    settings = Settings.from_env()

    storage = S3ObjectStorage(
        endpoint_url=settings.s3_endpoint_url,
        access_key=settings.s3_access_key,
        secret_key=settings.s3_secret_key,
        bucket=settings.s3_bucket,
        region=settings.s3_region,
    )
    backend_client = BackendStemCallbackClient(
        base_url=settings.backend_base_url,
        internal_api_key=settings.backend_internal_api_key,
    )
    workspace = TemporaryWorkspace(settings.workspace_root)

    if settings.worker_job_type == "audio-analysis":
        backend_audio_client = BackendAudioAnalysisCallbackClient(
            base_url=settings.backend_base_url,
            internal_api_key=settings.backend_internal_api_key,
        )
        analyzer = EssentiaMtgJamendoAnalyzer(
            settings.essentia_model_dir,
            auto_download=settings.essentia_auto_download_models,
        )
        pipeline = AudioAnalysisPipeline(
            storage=storage,
            analyzer=analyzer,
            backend_client=backend_audio_client,
            workspace=workspace,
        )

        consumer = RabbitMqConsumer(settings, pipeline.run, AudioAnalysisJob.from_message)
        consumer.start()
        return

    separator_registry = StemSeparatorRegistry()
    separator_registry.register(
        "demucs",
        DemucsStemSeparator(
            output_format=settings.demucs_output_format,
            device=settings.demucs_device,
        ),
    )

    pipeline = StemSeparationPipeline(
        storage=storage,
        separator_registry=separator_registry,
        backend_client=backend_client,
        workspace=workspace,
    )

    consumer = RabbitMqConsumer(settings, pipeline.run)
    consumer.start()


if __name__ == "__main__":
    main()
