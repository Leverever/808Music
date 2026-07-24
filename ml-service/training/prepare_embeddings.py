from __future__ import annotations

import argparse
import csv
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re

import numpy as np
from essentia.standard import MonoLoader, TensorflowPredictEffnetDiscogs


SAMPLE_RATE = 16_000
EMBEDDING_OUTPUT = "PartitionedCall:1"
SAFE_ID = re.compile(r"^[A-Za-z0-9_.-]+$")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Extract per-segment Discogs-EffNet embeddings for modern-head training."
    )
    parser.add_argument("--dataset", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--force", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    records = _read_dataset(args.dataset)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    _validate_existing_manifest(
        output_dir=args.output_dir,
        dataset_path=args.dataset,
        model_path=args.model,
        force=args.force,
    )

    model = TensorflowPredictEffnetDiscogs(
        graphFilename=str(args.model),
        output=EMBEDDING_OUTPUT,
    )

    for index, record in enumerate(records, start=1):
        track_id = record["track_id"].strip()
        if not SAFE_ID.fullmatch(track_id):
            raise ValueError(
                f"track_id may contain only letters, numbers, '.', '_' and '-': {track_id!r}"
            )

        output_path = args.output_dir / f"{track_id}.npy"
        if output_path.exists() and not args.force:
            print(f"[{index}/{len(records)}] skip {track_id}")
            continue

        audio_path = _resolve_audio_path(args.dataset, record["audio_path"])
        print(f"[{index}/{len(records)}] extract {track_id}: {audio_path}")
        audio = MonoLoader(
            filename=str(audio_path),
            sampleRate=SAMPLE_RATE,
            resampleQuality=4,
        )()
        embeddings = np.asarray(model(audio), dtype=np.float32)
        if embeddings.ndim != 2 or embeddings.shape[0] == 0:
            raise ValueError(f"No segment embeddings were produced for track {track_id}.")
        if embeddings.shape[1] != 1280:
            raise ValueError(
                f"Expected 1280 embedding dimensions for {track_id}, "
                f"received {embeddings.shape[1]}."
            )

        temporary_path = output_path.with_suffix(".npy.tmp")
        with temporary_path.open("wb") as file:
            np.save(file, embeddings)
        temporary_path.replace(output_path)

    _write_embedding_manifest(
        output_dir=args.output_dir,
        dataset_path=args.dataset,
        model_path=args.model,
        track_count=len(records),
    )


def _read_dataset(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as file:
        reader = csv.DictReader(file)
        required = {"track_id", "artist_id", "audio_path", "labels", "split"}
        missing = required.difference(reader.fieldnames or [])
        if missing:
            raise ValueError(f"Dataset is missing columns: {', '.join(sorted(missing))}")
        records = list(reader)

    if not records:
        raise ValueError("Dataset contains no tracks.")
    return records


def _resolve_audio_path(dataset_path: Path, value: str) -> Path:
    path = Path(value.strip())
    return path if path.is_absolute() else dataset_path.parent / path


def _write_embedding_manifest(
    output_dir: Path,
    dataset_path: Path,
    model_path: Path,
    track_count: int,
) -> None:
    manifest = {
        "embeddingModel": "discogs-effnet-bs64-1",
        "embeddingDimension": 1280,
        "embeddingOutput": EMBEDDING_OUTPUT,
        "sampleRate": SAMPLE_RATE,
        "trackCount": track_count,
        "datasetSha256": _sha256(dataset_path),
        "modelSha256": _sha256(model_path),
        "createdAt": datetime.now(timezone.utc).isoformat(),
    }
    with (output_dir / "embedding-manifest.json").open("w", encoding="utf-8") as file:
        json.dump(manifest, file, indent=2)
        file.write("\n")


def _validate_existing_manifest(
    output_dir: Path,
    dataset_path: Path,
    model_path: Path,
    force: bool,
) -> None:
    path = output_dir / "embedding-manifest.json"
    if force or not path.exists():
        return

    with path.open("r", encoding="utf-8") as file:
        manifest = json.load(file)
    expected = {
        "datasetSha256": _sha256(dataset_path),
        "modelSha256": _sha256(model_path),
    }
    mismatches = [
        key for key, value in expected.items() if manifest.get(key) != value
    ]
    if mismatches:
        raise ValueError(
            "Existing embeddings were produced from a different dataset or model "
            f"({', '.join(mismatches)} changed). Re-run with --force."
        )


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as file:
        while chunk := file.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    main()
