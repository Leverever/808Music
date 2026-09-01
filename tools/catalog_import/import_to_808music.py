#!/usr/bin/env python3
"""Import authorized audio from a yt-dlp source into an 808Music artist catalog."""

from __future__ import annotations

import argparse
import base64
import getpass
import hashlib
import importlib.metadata
import json
import mimetypes
import os
import re
import shutil
import sys
import tempfile
import time
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any, Iterable
from urllib.parse import parse_qs, urlparse


MANAGER_ROLES = {"Owner", "General Manager", "Streaming Manager"}
DOTNET_ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
TOKEN_ENVIRONMENT_VARIABLE = "MUSIC808_ACCESS_TOKEN"


class ImporterError(RuntimeError):
    """An expected, user-actionable importer error."""


class ApiError(ImporterError):
    def __init__(self, method: str, path: str, status_code: int, body: str):
        self.status_code = status_code
        self.body = body
        detail = body.strip()[:1000] or "No response body"
        super().__init__(f"808Music API {method} {path} returned {status_code}: {detail}")


@dataclass(frozen=True)
class SourceItem:
    key: str
    url: str
    title: str
    position: int


def parse_artist_id(value: str) -> int:
    value = value.strip()
    if value.isdigit() and int(value) > 0:
        return int(value)

    parsed = urlparse(value)
    query = parse_qs(parsed.query)
    for key in ("artistId", "artist_id", "id"):
        candidate = query.get(key, [""])[0]
        if candidate.isdigit() and int(candidate) > 0:
            return int(candidate)

    matches = re.findall(r"(?:^|/)(?:profile|artist|artists)/(\d+)(?:/|$)", parsed.path, re.I)
    if not matches:
        matches = re.findall(r"(?:^|/)(\d+)(?:/|$)", parsed.path)
    if matches and int(matches[-1]) > 0:
        return int(matches[-1])

    raise argparse.ArgumentTypeError(
        "artist profile must be a positive artist ID or a profile URL containing the ID"
    )


def clean_title(value: Any, position: int) -> str:
    title = re.sub(r"\s+", " ", str(value or "").replace("\x00", " ")).strip()
    if len(title) < 3:
        title = f"{title or 'Untitled'} (track {position})"
    return title[:200].rstrip()


def resolve_entry_url(entry: dict[str, Any]) -> str:
    for key in ("webpage_url", "original_url", "url"):
        value = entry.get(key)
        if isinstance(value, str) and urlparse(value).scheme in {"http", "https"}:
            return value

    extractor = str(entry.get("extractor_key") or entry.get("ie_key") or "").lower()
    media_id = str(entry.get("id") or entry.get("url") or "").strip()
    if media_id and "youtube" in extractor:
        return f"https://www.youtube.com/watch?v={media_id}"

    raise ImporterError(
        f"yt-dlp did not provide a usable URL for {entry.get('title') or 'one source item'}"
    )


def source_items(info: dict[str, Any], limit: int | None = None) -> list[SourceItem]:
    raw_entries: Iterable[dict[str, Any] | None]
    if info.get("entries") is None:
        raw_entries = [info]
    else:
        raw_entries = info["entries"]

    items: list[SourceItem] = []
    occurrences: dict[str, int] = {}
    for raw_entry in raw_entries:
        if not raw_entry:
            continue
        position = len(items) + 1
        url = resolve_entry_url(raw_entry)
        extractor = str(raw_entry.get("extractor_key") or raw_entry.get("ie_key") or "source")
        media_id = str(raw_entry.get("id") or hashlib.sha256(url.encode()).hexdigest()[:16])
        identity = f"{extractor}:{media_id}"
        occurrences[identity] = occurrences.get(identity, 0) + 1
        items.append(
            SourceItem(
                key=f"{identity}#{occurrences[identity]}",
                url=url,
                title=clean_title(raw_entry.get("title"), position),
                position=position,
            )
        )
        if limit is not None and len(items) >= limit:
            break

    if not items:
        raise ImporterError("yt-dlp found no downloadable media in the supplied source")
    return items


def jwt_roles(token: str) -> set[str]:
    """Read roles only for an early UX check; the API still validates the JWT."""
    try:
        payload_part = token.split(".")[1]
        payload_part += "=" * (-len(payload_part) % 4)
        payload = json.loads(base64.urlsafe_b64decode(payload_part).decode("utf-8"))
    except (IndexError, ValueError, UnicodeDecodeError, json.JSONDecodeError):
        return set()

    roles: set[str] = set()
    for key in ("role", DOTNET_ROLE_CLAIM):
        value = payload.get(key)
        if isinstance(value, str):
            roles.add(value)
        elif isinstance(value, list):
            roles.update(str(role) for role in value)
    return roles


def dependency_modules() -> tuple[Any, Any]:
    try:
        import requests  # type: ignore
    except ImportError as exc:
        raise ImporterError(
            "Python package 'requests' is missing; install tools/catalog_import/requirements.txt"
        ) from exc
    try:
        import yt_dlp  # type: ignore
    except ImportError as exc:
        raise ImporterError(
            "Python package 'yt-dlp' is missing; install tools/catalog_import/requirements.txt"
        ) from exc
    return requests, yt_dlp


def find_ffmpeg(ffmpeg_location: str | None) -> str | None:
    if ffmpeg_location:
        location = Path(ffmpeg_location).expanduser()
        executable = location / ("ffmpeg.exe" if os.name == "nt" else "ffmpeg") if location.is_dir() else location
        if not executable.is_file():
            raise ImporterError(f"ffmpeg was not found at {executable}")
        return str(location)
    if shutil.which("ffmpeg") is None:
        raise ImporterError("ffmpeg is not on PATH; install it or pass --ffmpeg-location")
    return None


def is_youtube_url(url: str) -> bool:
    hostname = (urlparse(url).hostname or "").lower().removeprefix("www.")
    return hostname in {"youtube.com", "music.youtube.com", "youtu.be"}


def configure_youtube_runtime(args: argparse.Namespace) -> None:
    if not is_youtube_url(args.source_url):
        return

    if args.deno_location:
        deno_path = args.deno_location.expanduser().resolve()
        if not deno_path.is_file():
            raise ImporterError(f"Deno was not found at {deno_path}")
    else:
        detected = shutil.which("deno")
        if detected is None:
            raise ImporterError(
                "YouTube imports require Deno 2.3+ on PATH. Install it with "
                "'winget install DenoLand.Deno', reopen PowerShell, and rerun."
            )
        deno_path = Path(detected).resolve()

    try:
        importlib.metadata.version("yt-dlp-ejs")
    except importlib.metadata.PackageNotFoundError as exc:
        raise ImporterError(
            "YouTube imports require yt-dlp-ejs. Upgrade this environment with "
            "'python -m pip install -U \"yt-dlp[default]\"'."
        ) from exc

    args.deno_location = deno_path


class Music808Client:
    def __init__(self, requests_module: Any, api_url: str, token: str, verify_tls: bool = True):
        self.requests = requests_module
        self.base_url = api_url.rstrip("/")
        self.verify_tls = verify_tls
        self.session = requests_module.Session()
        self.session.headers.update(
            {"Authorization": f"Bearer {token}", "Accept": "application/json"}
        )

    def _request(self, method: str, path: str, **kwargs: Any) -> Any:
        try:
            response = self.session.request(
                method,
                f"{self.base_url}{path}",
                timeout=(15, 3600),
                verify=self.verify_tls,
                **kwargs,
            )
        except self.requests.RequestException as exc:
            raise ImporterError(f"808Music API request failed: {exc}") from exc
        if response.status_code >= 400:
            raise ApiError(method, path, response.status_code, response.text)
        return response

    @staticmethod
    def _json(response: Any, context: str) -> Any:
        try:
            return response.json()
        except ValueError as exc:
            raise ImporterError(f"808Music returned invalid JSON while {context}") from exc

    def get_json(self, path: str) -> Any:
        response = self._request("GET", path)
        return self._json(response, f"requesting {path}")

    def verify_artist_access(self, artist_id: int) -> dict[str, Any]:
        artist = self.get_json(f"/api/ArtistGetByIdEndpoint/{artist_id}")
        memberships = self.get_json("/api/ArtistGetAllByUserEndpoint")
        membership = next(
            (entry for entry in memberships if int(entry.get("id", 0)) == artist_id), None
        )
        roles = jwt_roles(self.session.headers["Authorization"].removeprefix("Bearer "))
        if membership is None and not any(role.lower() == "admin" for role in roles):
            raise ImporterError(
                "the authenticated user is not a member of that artist profile; refresh the login "
                "token if access was granted recently"
            )
        if membership is not None and membership.get("role") not in MANAGER_ROLES:
            raise ImporterError(
                f"artist role '{membership.get('role')}' cannot upload tracks; required: "
                + ", ".join(sorted(MANAGER_ROLES))
            )
        return artist

    def verify_release_for_artist(self, release_id: int, artist_id: int) -> None:
        page = 1
        while True:
            response = self._request(
                "GET",
                "/api/v2/releases",
                params={"artistId": artist_id, "pageNumber": page, "pageSize": 50},
            )
            result = self._json(response, "checking the target release")
            if any(int(item.get("id", 0)) == release_id for item in result.get("items", [])):
                return
            if not result.get("hasNextPage"):
                raise ImporterError(
                    f"release {release_id} does not belong to artist {artist_id} or is unavailable"
                )
            page += 1

    def upload_track(self, artist_id: int, title: str, explicit: bool, mp3_path: Path) -> dict[str, Any]:
        with mp3_path.open("rb") as media:
            response = self._request(
                "POST",
                "/api/v2/tracks/upload",
                data={
                    "artistId": str(artist_id),
                    "title": title,
                    "isExplicit": str(explicit).lower(),
                },
                files={"masterFile": (mp3_path.name, media, "audio/mpeg")},
            )
        return self._json(response, "uploading a track")

    def create_release(
        self,
        artist_id: int,
        title: str,
        distributor: str,
        release_date: str,
        album_type_id: int,
        cover: Path | None,
    ) -> int:
        data = {
            "title": title,
            "distributor": distributor,
            "releaseDate": release_date,
            "albumTypeId": str(album_type_id),
            "artistId": str(artist_id),
        }
        if cover is None:
            response = self._request("POST", "/api/AlbumInsertOrUpdateEndpoint", data=data)
        else:
            content_type = mimetypes.guess_type(cover.name)[0] or "application/octet-stream"
            with cover.open("rb") as image:
                response = self._request(
                    "POST",
                    "/api/AlbumInsertOrUpdateEndpoint",
                    data=data,
                    files={"coverImage": (cover.name, image, content_type)},
                )
        result = self._json(response, "creating a release")
        release_id = int(result.get("id", 0))
        if release_id <= 0:
            raise ImporterError(
                "album creation succeeded but the API returned no ID; rebuild the backend with the "
                "AlbumInsertResponse.Id change included with this importer"
            )
        return release_id

    def attach_track(self, release_id: int, track_id: int, track_number: int) -> None:
        path = f"/api/v2/releases/{release_id}/tracks"
        try:
            self._request(
                "POST",
                path,
                json={
                    "trackId": track_id,
                    "discNumber": 1,
                    "trackNumber": track_number,
                    "titleOverride": None,
                    "isPrimaryRelease": True,
                },
            )
        except ApiError as exc:
            if exc.status_code != 409:
                raise
            # A resumed run may encounter an association committed before its state file was saved.
            self.get_json(f"{path}/{track_id}")


class Manifest:
    VERSION = 1
    REPLACE_ATTEMPTS = 6

    def __init__(self, path: Path, source_url: str, artist_id: int):
        self.path = path
        current_modified_ns = -1
        if path.exists():
            try:
                self.data = json.loads(path.read_text(encoding="utf-8"))
                current_modified_ns = path.stat().st_mtime_ns
            except (OSError, json.JSONDecodeError) as exc:
                raise ImporterError(f"could not read state file {path}: {exc}") from exc
        else:
            self.data = {
                "version": self.VERSION,
                "source_url": source_url,
                "artist_id": artist_id,
                "release_id": None,
                "items": {},
            }

        self._validate(self.data, source_url, artist_id, path)

        # A Windows file scanner, editor, or sync client can briefly lock the
        # destination between writing and replacing it. Recover a newer,
        # complete checkpoint left by an interrupted save before resuming.
        pending_paths = [path.with_suffix(path.suffix + ".tmp")]
        pending_paths.extend(path.parent.glob(f"{path.name}.*.tmp"))
        existing_pending = sorted(
            {candidate for candidate in pending_paths if candidate.exists()},
            key=lambda candidate: candidate.stat().st_mtime_ns,
            reverse=True,
        )
        for pending in existing_pending:
            if pending.stat().st_mtime_ns < current_modified_ns:
                continue
            try:
                candidate_data = json.loads(pending.read_text(encoding="utf-8"))
                self._validate(candidate_data, source_url, artist_id, pending)
            except (OSError, json.JSONDecodeError, ImporterError):
                continue
            self.data = candidate_data
            self._replace_with_retries(pending)
            break

    def _validate(
        self, data: dict[str, Any], source_url: str, artist_id: int, path: Path
    ) -> None:
        if data.get("source_url") != source_url or data.get("artist_id") != artist_id:
            raise ImporterError(
                f"state file {path} belongs to a different source or artist; use --state-file"
            )
        if data.get("version") != self.VERSION:
            raise ImporterError(f"state file {path} uses an unsupported format version")

    @property
    def release_id(self) -> int | None:
        value = self.data.get("release_id")
        return int(value) if value else None

    def set_release_id(self, release_id: int) -> None:
        existing = self.release_id
        if existing is not None and existing != release_id:
            raise ImporterError(
                f"state already refers to release {existing}, not requested release {release_id}"
            )
        self.data["release_id"] = release_id
        self.save()

    def item(self, key: str) -> dict[str, Any]:
        return self.data["items"].setdefault(key, {})

    def save(self) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary: Path | None = None
        try:
            with tempfile.NamedTemporaryFile(
                mode="w",
                encoding="utf-8",
                newline="\n",
                prefix=f"{self.path.name}.",
                suffix=".tmp",
                dir=self.path.parent,
                delete=False,
            ) as output:
                temporary = Path(output.name)
                output.write(json.dumps(self.data, indent=2, sort_keys=True))
                output.flush()
                os.fsync(output.fileno())
        except OSError as exc:
            if temporary is not None:
                temporary.unlink(missing_ok=True)
            raise ImporterError(f"could not write state checkpoint {self.path}: {exc}") from exc

        self._replace_with_retries(temporary)

    def _replace_with_retries(self, temporary: Path) -> None:
        for attempt in range(self.REPLACE_ATTEMPTS):
            try:
                os.replace(temporary, self.path)
                return
            except PermissionError as exc:
                if attempt + 1 == self.REPLACE_ATTEMPTS:
                    raise ImporterError(
                        f"could not commit state file {self.path} because Windows kept it locked; "
                        f"the recoverable checkpoint remains at {temporary}. Close any editor or "
                        "second importer using the file, then rerun."
                    ) from exc
                time.sleep(0.05 * (2**attempt))
            except OSError as exc:
                raise ImporterError(f"could not commit state file {self.path}: {exc}") from exc


def yt_dlp_common_options(args: argparse.Namespace, ffmpeg_location: str | None) -> dict[str, Any]:
    options: dict[str, Any] = {
        "quiet": not args.verbose,
        "no_warnings": not args.verbose,
        "cookiefile": str(args.cookies) if args.cookies else None,
        "retries": 3,
        "fragment_retries": 3,
    }
    if ffmpeg_location:
        options["ffmpeg_location"] = ffmpeg_location
    if args.deno_location:
        options["js_runtimes"] = {"deno": {"path": str(args.deno_location)}}
    if args.pot_server:
        options["extractor_args"] = {
            "youtube": {"player_client": ["mweb"]},
            "youtubepot-bgutilhttp": {"base_url": [args.pot_server.rstrip("/")]},
        }
    return options


def inspect_source(yt_dlp: Any, args: argparse.Namespace, ffmpeg_location: str | None) -> dict[str, Any]:
    options = yt_dlp_common_options(args, ffmpeg_location)
    options.update({"extract_flat": "in_playlist", "skip_download": True, "lazy_playlist": False})
    try:
        with yt_dlp.YoutubeDL(options) as ydl:
            result = ydl.extract_info(args.source_url, download=False)
    except Exception as exc:
        raise ImporterError(f"yt-dlp could not inspect the source: {exc}") from exc
    if not result:
        raise ImporterError("yt-dlp returned no metadata for the source")
    return result


def download_mp3(
    yt_dlp: Any,
    args: argparse.Namespace,
    ffmpeg_location: str | None,
    item: SourceItem,
    work_dir: Path,
) -> Path:
    work_dir.mkdir(parents=True, exist_ok=True)
    output_template = str(work_dir / f"item-{item.position:04d}.%(ext)s")
    mp3_path = work_dir / f"item-{item.position:04d}.mp3"
    for attempt in range(1, args.download_attempts + 1):
        options = yt_dlp_common_options(args, ffmpeg_location)
        options.update(
            {
                "format": "bestaudio/best",
                "outtmpl": output_template,
                "noplaylist": True,
                "overwrites": True,
                "postprocessors": [
                    {
                        "key": "FFmpegExtractAudio",
                        "preferredcodec": "mp3",
                        "preferredquality": args.mp3_quality,
                    },
                    {"key": "FFmpegMetadata", "add_metadata": True},
                ],
            }
        )
        try:
            # A new YoutubeDL instance refreshes the signed media URL and any
            # video-bound PO token after a transient Google Video Server 403.
            with yt_dlp.YoutubeDL(options) as ydl:
                ydl.extract_info(item.url, download=True)
            break
        except Exception as exc:
            if attempt == args.download_attempts:
                raise ImporterError(
                    f"yt-dlp/ffmpeg failed for '{item.title}' after "
                    f"{args.download_attempts} attempts: {exc}"
                ) from exc
            delay = args.download_retry_delay * attempt
            print(
                f"Download attempt {attempt}/{args.download_attempts} failed for "
                f"'{item.title}': {exc}. Retrying with a fresh media URL/token in "
                f"{delay:g} seconds...",
                file=sys.stderr,
            )
            time.sleep(delay)
    if not mp3_path.is_file() or mp3_path.stat().st_size == 0:
        raise ImporterError(f"ffmpeg did not produce the expected MP3 file {mp3_path}")
    return mp3_path


def token_from_args(args: argparse.Namespace) -> str:
    if args.token_file:
        try:
            token = args.token_file.read_text(encoding="utf-8").strip()
        except OSError as exc:
            raise ImporterError(f"could not read token file: {exc}") from exc
    else:
        token = os.environ.get(TOKEN_ENVIRONMENT_VARIABLE, "").strip()
    if not token and sys.stdin.isatty():
        token = getpass.getpass("808Music access token: ").strip()
    if not token:
        raise ImporterError(
            f"no access token supplied; set {TOKEN_ENVIRONMENT_VARIABLE} or use --token-file"
        )
    return token.removeprefix("Bearer ").strip()


def positive_int(value: str) -> int:
    parsed = int(value)
    if parsed <= 0:
        raise argparse.ArgumentTypeError("must be a positive integer")
    return parsed


def nonnegative_float(value: str) -> float:
    parsed = float(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("must be zero or greater")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Download authorized media with yt-dlp, convert it to MP3, and upload it to 808Music."
    )
    parser.add_argument("source_url", help="yt-dlp-supported album, playlist, or media URL")
    parser.add_argument(
        "--artist-profile",
        required=True,
        type=parse_artist_id,
        metavar="ID_OR_URL",
        help="target 808Music artist ID or profile URL",
    )
    parser.add_argument("--api-url", default="http://localhost:7000", help="808Music API origin")
    parser.add_argument("--token-file", type=Path, help="file containing only the 808Music JWT")
    parser.add_argument("--cookies", type=Path, help="Netscape cookie file for media you may access")
    parser.add_argument("--ffmpeg-location", help="ffmpeg executable or its containing directory")
    parser.add_argument("--deno-location", type=Path, help="Deno executable used for YouTube challenges")
    parser.add_argument(
        "--pot-server",
        nargs="?",
        const="http://127.0.0.1:4416",
        metavar="URL",
        help="use a bgutil POT server (default: http://127.0.0.1:4416) with YouTube's mweb client",
    )
    parser.add_argument(
        "--download-attempts",
        type=positive_int,
        default=3,
        help="whole-item download attempts with fresh media URLs/tokens (default: 3)",
    )
    parser.add_argument(
        "--download-retry-delay",
        type=nonnegative_float,
        default=5.0,
        metavar="SECONDS",
        help="base delay between whole-item download attempts (default: 5)",
    )
    parser.add_argument("--mp3-quality", default="0", help="FFmpeg MP3 VBR quality (0 best, 9 smallest)")
    parser.add_argument("--explicit", action="store_true", help="mark every imported track explicit")
    parser.add_argument("--release-id", type=positive_int, help="attach tracks to an existing 808Music release")
    parser.add_argument("--create-release", action="store_true", help="create a release from the source title")
    parser.add_argument("--release-title", help="override the title used with --create-release")
    parser.add_argument("--distributor", default="Direct import", help="release distributor")
    parser.add_argument("--release-date", type=date.fromisoformat, help="release date in YYYY-MM-DD form")
    parser.add_argument("--album-type-id", type=positive_int, default=4, help="808Music album type ID")
    parser.add_argument("--cover", type=Path, help="local JPG or PNG cover for --create-release")
    parser.add_argument("--limit", type=positive_int, help="import only the first N source items")
    parser.add_argument("--state-file", type=Path, help="resume-state JSON path")
    parser.add_argument("--work-dir", type=Path, help="temporary converted-audio directory")
    parser.add_argument("--keep-files", action="store_true", help="keep converted MP3s after upload")
    parser.add_argument("--continue-on-error", action="store_true", help="continue after an item fails")
    parser.add_argument("--dry-run", action="store_true", help="inspect and list without creating or uploading")
    parser.add_argument("--insecure", action="store_true", help="disable 808Music API TLS validation")
    parser.add_argument("--verbose", action="store_true", help="show yt-dlp diagnostic output")
    parser.add_argument(
        "--confirm-rights",
        action="store_true",
        help="confirm you own or are licensed to download, convert, and upload every item",
    )
    return parser


def validate_args(parser: argparse.ArgumentParser, args: argparse.Namespace) -> None:
    if not args.confirm_rights:
        parser.error("--confirm-rights is required")
    if args.release_id and args.create_release:
        parser.error("use either --release-id or --create-release, not both")
    if args.release_title and not args.create_release:
        parser.error("--release-title requires --create-release")
    if args.cover and not args.create_release:
        parser.error("--cover requires --create-release")
    if args.cover and (not args.cover.is_file() or args.cover.suffix.lower() not in {".jpg", ".jpeg", ".png"}):
        parser.error("--cover must be an existing JPG or PNG file")
    if len(args.distributor.strip()) < 3:
        parser.error("--distributor must be at least three characters")
    if args.mp3_quality not in {str(value) for value in range(10)}:
        parser.error("--mp3-quality must be from 0 through 9")
    if args.pot_server:
        parsed_pot_url = urlparse(args.pot_server)
        if parsed_pot_url.scheme not in {"http", "https"} or not parsed_pot_url.hostname:
            parser.error("--pot-server must be an HTTP or HTTPS URL")
        try:
            importlib.metadata.version("bgutil-ytdlp-pot-provider")
        except importlib.metadata.PackageNotFoundError:
            parser.error(
                "--pot-server requires bgutil-ytdlp-pot-provider; install it in the importer "
                "environment with python -m pip install -U bgutil-ytdlp-pot-provider"
            )


def run(args: argparse.Namespace) -> int:
    requests, yt_dlp = dependency_modules()
    ffmpeg_location = find_ffmpeg(args.ffmpeg_location)
    configure_youtube_runtime(args)
    token = token_from_args(args)
    artist_id = args.artist_profile

    digest = hashlib.sha256(f"{args.source_url}\0{artist_id}".encode()).hexdigest()[:12]
    runtime_root = Path(__file__).resolve().parent
    state_path = (args.state_file or runtime_root / ".state" / f"{digest}.json").resolve()
    work_dir = (args.work_dir or runtime_root / ".work" / digest).resolve()
    manifest = Manifest(state_path, args.source_url, artist_id)

    client = Music808Client(requests, args.api_url, token, verify_tls=not args.insecure)
    artist = client.verify_artist_access(artist_id)
    print(f"Authorized target: {artist.get('name', 'artist')} (ID {artist_id})")

    requested_release_id = args.release_id or manifest.release_id
    if requested_release_id is not None:
        client.verify_release_for_artist(requested_release_id, artist_id)

    info = inspect_source(yt_dlp, args, ffmpeg_location)
    items = source_items(info, args.limit)
    source_title = clean_title(info.get("title"), 1)
    print(f"Source: {source_title} ({len(items)} item{'s' if len(items) != 1 else ''})")

    for item in items:
        marker = "already uploaded" if manifest.item(item.key).get("track_id") else "pending"
        print(f"  {item.position:02d}. {item.title} [{marker}]")
    if args.dry_run:
        print("Dry run complete; no release was created and no media was downloaded or uploaded.")
        return 0

    release_id = manifest.release_id
    if args.release_id:
        manifest.set_release_id(args.release_id)
        release_id = args.release_id
    elif args.create_release and release_id is None:
        release_title = clean_title(args.release_title or info.get("title"), 1)
        release_id = client.create_release(
            artist_id=artist_id,
            title=release_title,
            distributor=args.distributor.strip(),
            release_date=(args.release_date or date.today()).isoformat(),
            album_type_id=args.album_type_id,
            cover=args.cover.resolve() if args.cover else None,
        )
        manifest.set_release_id(release_id)
        print(f"Created release '{release_title}' (ID {release_id}).")

    completed = 0
    failed: list[str] = []
    for item in items:
        state = manifest.item(item.key)
        try:
            track_id = int(state.get("track_id", 0))
            mp3_path: Path | None = None
            if track_id <= 0:
                print(f"[{item.position}/{len(items)}] Downloading and converting: {item.title}")
                mp3_path = download_mp3(yt_dlp, args, ffmpeg_location, item, work_dir)
                print(f"[{item.position}/{len(items)}] Uploading: {item.title}")
                upload = client.upload_track(artist_id, item.title, args.explicit, mp3_path)
                track_id = int(upload["id"])
                state.update(
                    {
                        "title": item.title,
                        "position": item.position,
                        "source_url": item.url,
                        "track_id": track_id,
                        "attached": False,
                    }
                )
                manifest.save()
                if not args.keep_files:
                    try:
                        mp3_path.unlink(missing_ok=True)
                    except OSError as exc:
                        print(f"Warning: could not remove {mp3_path}: {exc}", file=sys.stderr)

            if release_id is not None and not state.get("attached"):
                print(f"[{item.position}/{len(items)}] Attaching track {track_id} to release {release_id}")
                client.attach_track(release_id, track_id, item.position)
                state["attached"] = True
                manifest.save()

            completed += 1
            print(f"[{item.position}/{len(items)}] Complete: track ID {track_id}")
        except (ImporterError, KeyError, ValueError, OSError) as exc:
            failed.append(f"{item.title}: {exc}")
            print(f"[{item.position}/{len(items)}] FAILED: {exc}", file=sys.stderr)
            if not args.continue_on_error:
                break

    print(f"Import result: {completed} completed, {len(failed)} failed. State: {state_path}")
    if failed:
        print("Failed items:", file=sys.stderr)
        for failure in failed:
            print(f"  - {failure}", file=sys.stderr)
        return 1
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    validate_args(parser, args)
    try:
        return run(args)
    except ImporterError as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print("Interrupted; rerun with the same arguments to resume.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
