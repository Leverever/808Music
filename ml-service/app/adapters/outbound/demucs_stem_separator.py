from pathlib import Path
import hashlib
import subprocess
import sys
import wave

from app.domain import SeparatedStem, StemSeparationJob


class DemucsStemSeparator:
    def __init__(self, output_format: str = "wav", device: str = "cpu") -> None:
        self._output_format = output_format.strip().lower()
        self._device = device.strip().lower()

    def separate(
        self,
        job: StemSeparationJob,
        input_path: Path,
        output_dir: Path,
    ) -> list[SeparatedStem]:
        if self._device == "cuda":
            self._ensure_cuda_available()

        command = [
            sys.executable,
            "-m",
            "demucs",
            "--name",
            job.model_name or "htdemucs",
            "--device",
            self._device,
            "--out",
            str(output_dir),
        ]

        if self._output_format == "mp3":
            command.append("--mp3")

        if job.stem_profile.lower() in {"two-stem-vocals", "vocals"}:
            command.extend(["--two-stems", "vocals"])

        command.append(str(input_path))

        subprocess.run(command, check=True)

        song_output_dir = output_dir / (job.model_name or "htdemucs") / input_path.stem
        if not song_output_dir.exists():
            raise FileNotFoundError(f"Demucs output folder was not found: {song_output_dir}")

        return [
            self._read_stem(stem_path)
            for stem_path in sorted(song_output_dir.iterdir())
            if stem_path.is_file() and stem_path.suffix.lower() in {".wav", ".mp3"}
        ]

    def _read_stem(self, stem_path: Path) -> SeparatedStem:
        stem_type = self._map_stem_type(stem_path.stem)
        codec = stem_path.suffix.lstrip(".").lower()
        size_bytes = stem_path.stat().st_size
        checksum = self._sha256(stem_path)
        duration_ms, sample_rate, channels = self._wav_metadata(stem_path)

        return SeparatedStem(
            stem_type=stem_type,
            path=stem_path,
            content_type=self._content_type(stem_path),
            codec=codec,
            size_bytes=size_bytes,
            duration_ms=duration_ms,
            sample_rate=sample_rate,
            bitrate_kbps=None,
            channels=channels,
            checksum_sha256=checksum,
        )

    @staticmethod
    def _map_stem_type(value: str) -> str:
        mapping = {
            "vocals": "Vocals",
            "drums": "Drums",
            "bass": "Bass",
            "other": "Other",
            "no_vocals": "Instrumental",
        }

        key = value.strip().lower()
        if key not in mapping:
            raise ValueError(f"Unsupported Demucs stem output: {value}")

        return mapping[key]

    @staticmethod
    def _content_type(path: Path) -> str:
        if path.suffix.lower() == ".mp3":
            return "audio/mpeg"

        return "audio/wav"

    @staticmethod
    def _ensure_cuda_available() -> None:
        try:
            import torch
        except ImportError as exc:
            raise RuntimeError("CUDA was requested, but PyTorch is not installed.") from exc

        if not torch.cuda.is_available():
            raise RuntimeError(
                "CUDA was requested, but PyTorch cannot see a GPU. "
                "Check the NVIDIA driver, NVIDIA Container Toolkit, and Docker GPU settings."
            )

    @staticmethod
    def _sha256(path: Path) -> str:
        digest = hashlib.sha256()
        with path.open("rb") as file:
            for chunk in iter(lambda: file.read(1024 * 1024), b""):
                digest.update(chunk)

        return digest.hexdigest()

    @staticmethod
    def _wav_metadata(path: Path) -> tuple[int | None, int | None, int | None]:
        if path.suffix.lower() != ".wav":
            return None, None, None

        with wave.open(str(path), "rb") as wav_file:
            frames = wav_file.getnframes()
            sample_rate = wav_file.getframerate()
            channels = wav_file.getnchannels()

        duration_ms = int(frames / sample_rate * 1000) if sample_rate else None
        return duration_ms, sample_rate, channels
