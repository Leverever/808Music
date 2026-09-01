from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

import boto3
import torch


TRACKS = [
    {
        "track_id": 17,
        "catalog_duration_seconds": 26,
        "object_key": "tracks/17/masters/d24221bb5e3cb3138e4dea5baa289286444d0c844cb0b23a73f4623b6d177ae5.mp3",
    },
    {
        "track_id": 95,
        "catalog_duration_seconds": 95,
        "object_key": "tracks/95/masters/5fb26defc7fcf7d88d4101375ed8b20c87ec2579a1696be618e78030d3e298e4.mp3",
    },
]


def s3_client():
    return boto3.client(
        "s3",
        endpoint_url=os.environ["S3_ENDPOINT_URL"],
        aws_access_key_id=os.environ["S3_ACCESS_KEY"],
        aws_secret_access_key=os.environ["S3_SECRET_KEY"],
        region_name=os.environ.get("S3_REGION", "eu-central-1"),
    )


def download_inputs(root: Path) -> list[dict[str, object]]:
    input_dir = root / "inputs"
    input_dir.mkdir(parents=True, exist_ok=True)
    client = s3_client()
    bucket = os.environ["S3_BUCKET"]
    result = []
    for track in TRACKS:
        local_path = input_dir / f"track-{track['track_id']}.mp3"
        if not local_path.exists():
            client.download_file(bucket, str(track["object_key"]), str(local_path))
        result.append({**track, "local_path": str(local_path), "input_size_bytes": local_path.stat().st_size})
    return result


def run_case(root: Path, track: dict[str, object], device: str, profile: str, iteration: int):
    case_key = f"track-{track['track_id']}-{device}-{profile}-{iteration}"
    output_dir = (root / "outputs" / case_key).resolve()
    expected_root = (root / "outputs").resolve()
    if expected_root not in output_dir.parents:
        raise RuntimeError(f"Unsafe benchmark output path: {output_dir}")
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True)

    command = [
        sys.executable,
        "-m",
        "demucs",
        "--name",
        "htdemucs",
        "--device",
        device,
        "--out",
        str(output_dir),
        "--mp3",
    ]
    if profile == "two-stem-vocals":
        command.extend(["--two-stems", "vocals"])
    command.append(str(track["local_path"]))

    started = time.perf_counter()
    completed = subprocess.run(command, text=True, capture_output=True)
    duration = time.perf_counter() - started
    files = list(output_dir.rglob("*.mp3"))
    result = {
        "track_id": int(track["track_id"]),
        "catalog_duration_seconds": int(track["catalog_duration_seconds"]),
        "input_size_bytes": int(track["input_size_bytes"]),
        "device": device,
        "profile": profile,
        "iteration": iteration,
        "success": completed.returncode == 0,
        "wall_time_seconds": round(duration, 3),
        "real_time_factor": round(duration / max(1, int(track["catalog_duration_seconds"])), 4),
        "output_file_count": len(files),
        "output_size_bytes": sum(path.stat().st_size for path in files),
    }
    if completed.returncode != 0:
        result["error_tail"] = completed.stderr[-2000:]

    shutil.rmtree(output_dir)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default="/work/thesis-benchmark")
    parser.add_argument("--iterations", type=int, default=1)
    args = parser.parse_args()

    root = Path(args.root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    tracks = download_inputs(root)
    results = []
    for device in ("cuda", "cpu"):
        for profile in ("four-stem", "two-stem-vocals"):
            for track in tracks:
                for iteration in range(1, args.iterations + 1):
                    result = run_case(root, track, device, profile, iteration)
                    results.append(result)
                    print(json.dumps(result), flush=True)

    payload = {
        "model": "htdemucs",
        "demucs_version": "4.1.0",
        "torch_version": torch.__version__,
        "cuda_available": torch.cuda.is_available(),
        "gpu": torch.cuda.get_device_name(0) if torch.cuda.is_available() else None,
        "cpu": os.uname().machine,
        "output_format": "mp3",
        "tracks": [{key: value for key, value in track.items() if key != "local_path"} for track in tracks],
        "results": results,
    }
    output_path = root / "demucs_benchmark.json"
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"Wrote {output_path}")


if __name__ == "__main__":
    main()
