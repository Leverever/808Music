import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


SCRIPT = Path(__file__).parents[1] / "import_to_808music.py"
SPEC = importlib.util.spec_from_file_location("catalog_importer", SCRIPT)
assert SPEC and SPEC.loader
IMPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = IMPORTER
SPEC.loader.exec_module(IMPORTER)


class ArtistIdTests(unittest.TestCase):
    def test_accepts_id(self):
        self.assertEqual(42, IMPORTER.parse_artist_id("42"))

    def test_accepts_profile_url(self):
        self.assertEqual(
            42,
            IMPORTER.parse_artist_id("http://localhost:4200/listener/profile/42"),
        )

    def test_accepts_query_id(self):
        self.assertEqual(42, IMPORTER.parse_artist_id("https://music.test/artist?artistId=42"))


class SourceItemTests(unittest.TestCase):
    def test_recognizes_youtube_hosts(self):
        self.assertTrue(IMPORTER.is_youtube_url("https://www.youtube.com/watch?v=abc"))
        self.assertTrue(IMPORTER.is_youtube_url("https://youtu.be/abc"))
        self.assertFalse(IMPORTER.is_youtube_url("https://media.test/abc"))

    def test_builds_youtube_url_for_flat_entry(self):
        entry = {"id": "abc", "url": "abc", "ie_key": "Youtube", "title": "A title"}
        self.assertEqual("https://www.youtube.com/watch?v=abc", IMPORTER.resolve_entry_url(entry))

    def test_repeated_media_get_distinct_resume_keys(self):
        info = {
            "entries": [
                {"id": "abc", "webpage_url": "https://media.test/abc", "title": "One"},
                {"id": "abc", "webpage_url": "https://media.test/abc", "title": "One again"},
            ]
        }
        items = IMPORTER.source_items(info)
        self.assertNotEqual(items[0].key, items[1].key)


class YoutubeOptionsTests(unittest.TestCase):
    def test_configures_mweb_and_bgutil_server(self):
        args = SimpleNamespace(
            verbose=False,
            cookies=None,
            deno_location=None,
            pot_server="http://127.0.0.1:4416/",
        )

        options = IMPORTER.yt_dlp_common_options(args, None)

        self.assertEqual(["mweb"], options["extractor_args"]["youtube"]["player_client"])
        self.assertEqual(
            ["http://127.0.0.1:4416"],
            options["extractor_args"]["youtubepot-bgutilhttp"]["base_url"],
        )

    def test_retries_download_with_a_new_youtube_dl_instance(self):
        calls = 0

        class FakeYoutubeDL:
            def __init__(self, options):
                self.options = options

            def __enter__(self):
                return self

            def __exit__(self, *_):
                return False

            def extract_info(self, _url, download):
                nonlocal calls
                calls += 1
                self.assert_download(download)
                if calls == 1:
                    raise RuntimeError("HTTP Error 403: Forbidden")
                Path(self.options["outtmpl"].replace("%(ext)s", "mp3")).write_bytes(b"mp3")

            @staticmethod
            def assert_download(download):
                if not download:
                    raise AssertionError("expected a media download")

        fake_module = SimpleNamespace(YoutubeDL=FakeYoutubeDL)
        args = SimpleNamespace(
            verbose=False,
            cookies=None,
            deno_location=None,
            pot_server="http://127.0.0.1:4416",
            mp3_quality="0",
            download_attempts=3,
            download_retry_delay=5.0,
        )
        item = IMPORTER.SourceItem("source:item#1", "https://media.test/item", "Item", 1)

        with tempfile.TemporaryDirectory() as directory:
            with mock.patch.object(IMPORTER.time, "sleep") as sleep:
                result = IMPORTER.download_mp3(fake_module, args, None, item, Path(directory))

        self.assertEqual(2, calls)
        sleep.assert_called_once_with(5.0)
        self.assertEqual("item-0001.mp3", result.name)


class ManifestTests(unittest.TestCase):
    def test_persists_track_and_release_state(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            manifest = IMPORTER.Manifest(path, "https://media.test/list", 7)
            manifest.set_release_id(9)
            manifest.item("source:item#1")["track_id"] = 11
            manifest.save()

            loaded = IMPORTER.Manifest(path, "https://media.test/list", 7)
            self.assertEqual(9, loaded.release_id)
            self.assertEqual(11, loaded.item("source:item#1")["track_id"])

    def test_rejects_state_for_another_artist(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            IMPORTER.Manifest(path, "https://media.test/list", 7).save()
            with self.assertRaises(IMPORTER.ImporterError):
                IMPORTER.Manifest(path, "https://media.test/list", 8)

    def test_retries_when_windows_temporarily_locks_state_file(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            manifest = IMPORTER.Manifest(path, "https://media.test/list", 7)
            real_replace = IMPORTER.os.replace
            attempts = 0

            def temporarily_locked(source, destination):
                nonlocal attempts
                attempts += 1
                if attempts < 3:
                    raise PermissionError(5, "Access is denied", destination)
                return real_replace(source, destination)

            with mock.patch.object(IMPORTER.os, "replace", side_effect=temporarily_locked):
                with mock.patch.object(IMPORTER.time, "sleep") as sleep:
                    manifest.save()

            self.assertEqual(3, attempts)
            self.assertEqual(2, sleep.call_count)
            self.assertTrue(path.exists())

    def test_recovers_newer_complete_checkpoint(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "state.json"
            manifest = IMPORTER.Manifest(path, "https://media.test/list", 7)
            manifest.item("source:item#1")["attached"] = False
            manifest.save()

            pending = path.with_suffix(path.suffix + ".tmp")
            manifest.item("source:item#1")["attached"] = True
            pending.write_text(
                IMPORTER.json.dumps(manifest.data, indent=2, sort_keys=True),
                encoding="utf-8",
            )
            pending.touch()

            recovered = IMPORTER.Manifest(path, "https://media.test/list", 7)

            self.assertTrue(recovered.item("source:item#1")["attached"])
            self.assertFalse(pending.exists())


if __name__ == "__main__":
    unittest.main()
