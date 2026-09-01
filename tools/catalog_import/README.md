# Authorized catalog importer

This tool uses `yt-dlp` and FFmpeg to convert every item in a supported album,
playlist, or single-media URL to MP3, then uploads it through 808Music's normal
authenticated artist-track endpoint. The API remains the authority: only an
Admin, Owner, General Manager, or Streaming Manager for the selected artist can
upload.

Use it only for media you own or are explicitly licensed to download, convert,
and publish to the selected 808Music profile. It does not bypass DRM or other
access controls. You are also responsible for the source site's terms.

## Setup

Python 3.10+, FFmpeg/ffprobe, and Deno 2.3+ must be installed. Current YouTube
support in `yt-dlp` uses Deno to solve JavaScript playback challenges. Install
Deno on Windows, then open a new PowerShell window:

```powershell
winget install --id DenoLand.Deno --exact --source winget
deno --version
```

Create or update the importer environment from the repository root. The
`default` yt-dlp dependency group installs the matching `yt-dlp-ejs` scripts:

```powershell
python -m venv .venv-catalog-import
.\.venv-catalog-import\Scripts\Activate.ps1
python -m pip install --upgrade -r tools\catalog_import\requirements.txt
```

Put a current 808Music access token in an environment variable. Do not place the
token directly on the command line, where other local processes may see it:

```powershell
$env:MUSIC808_ACCESS_TOKEN = "paste-the-JWT-here"
```

## Examples

Preview an import without downloading or changing 808Music:

```powershell
python tools\catalog_import\import_to_808music.py `
  "https://example.test/your-authorized-playlist" `
  --artist-profile "http://localhost:4200/listener/profile/42" `
  --confirm-rights `
  --dry-run
```

Upload the tracks directly to the artist profile:

```powershell
python tools\catalog_import\import_to_808music.py `
  "https://example.test/your-authorized-playlist" `
  --artist-profile 42 `
  --confirm-rights
```

Create an 808Music release named after the source and attach the imported tracks
in source order:

```powershell
python tools\catalog_import\import_to_808music.py `
  "https://example.test/your-authorized-album" `
  --artist-profile 42 `
  --create-release `
  --distributor "Your distributor" `
  --release-date 2026-08-16 `
  --cover .\cover.jpg `
  --confirm-rights
```

To attach tracks to an album already created in 808Music, use `--release-id 123`
instead of `--create-release`.

For a source that requires your normal logged-in access, export a Netscape-format
cookie file and pass `--cookies path\to\cookies.txt`. Only do this for media the
account is authorized to retrieve.

### YouTube PO-token server

For repeated YouTube HTTP 403 responses, install the bgutil provider plugin into
the same Python environment as the importer:

```powershell
python -m pip install -U bgutil-ytdlp-pot-provider
```

With the provider server running, add `--pot-server` to the normal import command.
This selects YouTube's `mweb` client and uses `http://127.0.0.1:4416` by default:

```powershell
python tools\catalog_import\import_to_808music.py `
  "https://youtube.com/playlist?list=YOUR_PLAYLIST_ID" `
  --artist-profile 42 `
  --confirm-rights `
  --pot-server
```

For another address, use a value such as `--pot-server http://127.0.0.1:8080`.
Add `--verbose` and confirm that the output contains
`Retrieved a gvs PO Token for mweb client`.

The importer retries a failed media download up to three times by default. Each
attempt creates a fresh yt-dlp session so signed URLs and video-bound PO tokens
are refreshed. Customize this with `--download-attempts` and
`--download-retry-delay`.

## Recovery and behavior

- MP3 conversion uses FFmpeg VBR quality 0 by default; choose 0 through 9 with
  `--mp3-quality`.
- A source-specific state file is written under `.state`. Rerunning the same
  command skips confirmed uploads and resumes release attachment.
- Converted files are removed after each confirmed upload unless `--keep-files`
  is set.
- A lost connection during an upload is ambiguous because the current upload API
  has no idempotency key. Check the artist catalog before retrying that item to
  avoid a duplicate.
- Long imports may outlive the JWT. Set a fresh token and rerun; completed items
  remain in the state file.
- Use `--explicit` only when every track in the source should carry that flag.

Run `python tools\catalog_import\import_to_808music.py --help` for all options.
