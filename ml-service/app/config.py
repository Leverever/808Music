from dataclasses import dataclass
from pathlib import Path
import os


def _env(name: str, default: str) -> str:
    value = os.getenv(name)
    return value if value is not None and value != "" else default


def _env_bool(name: str, default: bool) -> bool:
    value = os.getenv(name)
    if value is None or value == "":
        return default

    return value.strip().lower() in {"1", "true", "yes", "y", "on"}


@dataclass(frozen=True)
class Settings:
    rabbitmq_host: str
    rabbitmq_port: int
    rabbitmq_username: str
    rabbitmq_password: str
    rabbitmq_virtual_host: str
    rabbitmq_exchange: str
    rabbitmq_queue: str
    rabbitmq_routing_key: str
    rabbitmq_prefetch_count: int
    rabbitmq_heartbeat: int
    worker_job_type: str

    s3_endpoint_url: str
    s3_access_key: str
    s3_secret_key: str
    s3_bucket: str
    s3_region: str

    backend_base_url: str
    backend_internal_api_key: str

    workspace_root: Path
    essentia_model_dir: Path
    essentia_auto_download_models: bool
    demucs_output_format: str
    demucs_device: str

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            rabbitmq_host=_env("RABBITMQ_HOST", "localhost"),
            rabbitmq_port=int(_env("RABBITMQ_PORT", "5672")),
            rabbitmq_username=_env("RABBITMQ_USERNAME", "808music"),
            rabbitmq_password=_env("RABBITMQ_PASSWORD", "808music_dev_password"),
            rabbitmq_virtual_host=_env("RABBITMQ_VIRTUAL_HOST", "/"),
            rabbitmq_exchange=_env("RABBITMQ_EXCHANGE", "808music"),
            rabbitmq_queue=_env("RABBITMQ_QUEUE", "ml.stems.separation"),
            rabbitmq_routing_key=_env("RABBITMQ_ROUTING_KEY", "ml.stems.separate"),
            rabbitmq_prefetch_count=int(_env("RABBITMQ_PREFETCH_COUNT", "1")),
            rabbitmq_heartbeat=int(_env("RABBITMQ_HEARTBEAT", "0")),
            worker_job_type=_env("WORKER_JOB_TYPE", "stem-separation"),
            s3_endpoint_url=_env("S3_ENDPOINT_URL", "http://localhost:9000"),
            s3_access_key=_env("S3_ACCESS_KEY", "808music"),
            s3_secret_key=_env("S3_SECRET_KEY", "808music_dev_password"),
            s3_bucket=_env("S3_BUCKET", "808music-media"),
            s3_region=_env("S3_REGION", "eu-central-1"),
            backend_base_url=_env("BACKEND_BASE_URL", "http://localhost:7000"),
            backend_internal_api_key=_env("BACKEND_INTERNAL_API_KEY", "dev-internal-api-key"),
            workspace_root=Path(_env("WORKSPACE_ROOT", "/tmp/808music-ml")),
            essentia_model_dir=Path(_env("ESSENTIA_MODEL_DIR", "/models/essentia")),
            essentia_auto_download_models=_env_bool("ESSENTIA_AUTO_DOWNLOAD_MODELS", True),
            demucs_output_format=_env("DEMUCS_OUTPUT_FORMAT", "wav"),
            demucs_device=_env("DEMUCS_DEVICE", "cpu"),
        )
