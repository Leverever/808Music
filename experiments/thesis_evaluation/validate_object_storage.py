from __future__ import annotations

import argparse
import csv
import json
import os
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import boto3


def rows(path: Path):
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        yield from csv.DictReader(handle)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True)
    parser.add_argument("--stems", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    client = boto3.client(
        "s3",
        endpoint_url=os.environ["S3_ENDPOINT_URL"],
        aws_access_key_id=os.environ["S3_ACCESS_KEY"],
        aws_secret_access_key=os.environ["S3_SECRET_KEY"],
        region_name=os.environ.get("S3_REGION", "eu-central-1"),
    )
    bucket = os.environ["S3_BUCKET"]

    catalog_keys = sorted({row["TrackPath"] for row in rows(Path(args.catalog)) if row.get("TrackPath")})
    stem_rows = list(rows(Path(args.stems)))
    stem_keys = sorted({row["ObjectKey"] for row in stem_rows if row.get("ObjectKey")})
    recorded_sizes = {
        row["ObjectKey"]: int(row["SizeBytes"])
        for row in stem_rows
        if row.get("ObjectKey") and row.get("SizeBytes") and row["SizeBytes"].isdigit()
    }

    def inspect(key: str):
        try:
            response = client.head_object(Bucket=bucket, Key=key)
            return key, True, int(response["ContentLength"]), None
        except Exception as error:  # the error type depends on the S3-compatible client
            return key, False, None, type(error).__name__

    with ThreadPoolExecutor(max_workers=16) as executor:
        master_results = list(executor.map(inspect, catalog_keys))
        stem_results = list(executor.map(inspect, stem_keys))

    missing_masters = [key for key, exists, _, _ in master_results if not exists]
    missing_stems = [key for key, exists, _, _ in stem_results if not exists]
    size_mismatches = []
    for key, exists, actual_size, _ in stem_results:
        recorded_size = recorded_sizes.get(key)
        if exists and recorded_size and actual_size != recorded_size:
            size_mismatches.append(
                {"objectKey": key, "recordedSize": recorded_size, "actualSize": actual_size}
            )

    summary = {
        "bucket": bucket,
        "unique_master_keys": len(catalog_keys),
        "reachable_master_keys": len(catalog_keys) - len(missing_masters),
        "missing_master_count": len(missing_masters),
        "unique_stem_keys": len(stem_keys),
        "reachable_stem_keys": len(stem_keys) - len(missing_stems),
        "missing_stem_count": len(missing_stems),
        "stem_records_with_nonzero_recorded_size": len(recorded_sizes),
        "stem_size_mismatch_count": len(size_mismatches),
        "missing_master_keys": missing_masters,
        "missing_stem_keys": missing_stems,
        "stem_size_mismatches": size_mismatches,
    }
    Path(args.output).write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
