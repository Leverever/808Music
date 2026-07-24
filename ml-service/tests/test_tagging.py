import json
from pathlib import Path
import tempfile
import unittest

from app.domain.tagging import (
    TaggingPolicy,
    load_custom_head_specs,
    parse_discogs_class,
    select_ranked_labels,
)


class TaggingPolicyTests(unittest.TestCase):
    def test_selects_top_k_with_per_label_thresholds(self) -> None:
        policy = TaggingPolicy(
            top_k=2,
            min_score=0.20,
            thresholds={"phonk": 0.70},
        )

        selected = select_ranked_labels(
            ["house", "phonk", "drill", "noise"],
            [0.60, 0.69, 0.80, float("nan")],
            policy,
        )

        self.assertEqual(["drill", "house"], [item.label for item in selected])

    def test_rejects_score_count_mismatch(self) -> None:
        with self.assertRaisesRegex(ValueError, "2 scores for 1 labels"):
            select_ranked_labels(["house"], [0.7, 0.2], TaggingPolicy())

    def test_validates_probability_range(self) -> None:
        with self.assertRaisesRegex(ValueError, "between 0 and 1"):
            TaggingPolicy(min_score=1.1)


class DiscogsClassTests(unittest.TestCase):
    def test_splits_parent_into_namespace_and_leaf_label(self) -> None:
        parsed = parse_discogs_class("Funk / Soul---Contemporary R&B")

        self.assertEqual("discogs.funk_soul", parsed.namespace)
        self.assertEqual("Contemporary R&B", parsed.label)
        self.assertEqual("Funk / Soul", parsed.category)

    def test_supports_class_without_parent(self) -> None:
        parsed = parse_discogs_class("Phonk")

        self.assertEqual("discogs.style", parsed.namespace)
        self.assertEqual("Phonk", parsed.label)


class CustomHeadManifestTests(unittest.TestCase):
    def test_loads_only_enabled_heads(self) -> None:
        document = {
            "heads": [
                {
                    "namespace": "modern_genre",
                    "modelName": "modern-1",
                    "enabled": True,
                    "topK": 4,
                    "minScore": 0.25,
                },
                {
                    "namespace": "experimental",
                    "modelName": "experimental-1",
                    "enabled": False,
                },
            ]
        }

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "custom-heads.json"
            path.write_text(json.dumps(document), encoding="utf-8")

            specs = load_custom_head_specs(path)

        self.assertEqual(1, len(specs))
        self.assertEqual("modern_genre", specs[0].namespace)
        self.assertEqual(4, specs[0].policy.top_k)
        self.assertEqual(0.25, specs[0].policy.min_score)

    def test_missing_manifest_means_no_custom_heads(self) -> None:
        self.assertEqual([], load_custom_head_specs(Path("does-not-exist.json")))


if __name__ == "__main__":
    unittest.main()
