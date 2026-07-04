from pathlib import Path

import boto3


class S3ObjectStorage:
    def __init__(
        self,
        endpoint_url: str,
        access_key: str,
        secret_key: str,
        bucket: str,
        region: str,
    ) -> None:
        self._bucket = bucket
        self._client = boto3.client(
            "s3",
            endpoint_url=endpoint_url,
            aws_access_key_id=access_key,
            aws_secret_access_key=secret_key,
            region_name=region,
        )

    def download(self, object_key: str, destination: Path) -> None:
        destination.parent.mkdir(parents=True, exist_ok=True)
        self._client.download_file(self._bucket, object_key, str(destination))

    def upload(self, local_path: Path, object_key: str, content_type: str) -> None:
        self._client.upload_file(
            str(local_path),
            self._bucket,
            object_key,
            ExtraArgs={"ContentType": content_type},
        )
