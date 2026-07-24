import sys
import types
from pathlib import Path
import json
import tempfile
import unittest

import numpy as np


class FakePredictor:
    def __init__(self, **parameters) -> None:
        self.parameters = parameters


fake_standard = types.ModuleType("essentia.standard")
fake_standard.MonoLoader = FakePredictor
fake_standard.TensorflowPredictEffnetDiscogs = FakePredictor
fake_standard.TensorflowPredict2D = FakePredictor
sys.modules.setdefault("essentia", types.ModuleType("essentia"))
sys.modules.setdefault("essentia.standard", fake_standard)

from app.adapters.outbound.essentia_mtg_jamendo_analyzer import (  # noqa: E402
    EssentiaMtgJamendoAnalyzer,
)
from app.domain import TaggingPolicy  # noqa: E402


class AnalyzerDiscogsTagTests(unittest.TestCase):
    def setUp(self) -> None:
        self.analyzer = object.__new__(EssentiaMtgJamendoAnalyzer)
        self.analyzer._discogs_labels = [
            "Electronic---Hardstyle",
            "Non-Music---Speech",
            "Pop---K-pop",
        ]
        self.analyzer._discogs_policy = TaggingPolicy(top_k=2, min_score=0.15)

    def test_emits_leaf_labels_and_excludes_non_music(self) -> None:
        tags = self.analyzer._discogs_tags(
            np.asarray([0.80, 0.99, 0.60], dtype=np.float32)
        )

        self.assertEqual(
            [
                ("discogs.electronic", "Hardstyle"),
                ("discogs.pop", "K-pop"),
            ],
            [(tag.namespace, tag.label) for tag in tags],
        )

    def test_rejects_classifier_metadata_mismatch(self) -> None:
        with self.assertRaisesRegex(ValueError, "2 scores for 3 labels"):
            self.analyzer._discogs_tags(
                np.asarray([0.80, 0.60], dtype=np.float32)
            )

    def test_uses_schema_prediction_output(self) -> None:
        metadata = {
            "schema": {
                "outputs": [
                    {"name": "logits"},
                    {
                        "name": "probabilities",
                        "output_purpose": "predictions",
                    },
                ]
            }
        }

        output = self.analyzer._schema_node_name(
            metadata,
            collection="outputs",
            default="model/Sigmoid",
            purpose="predictions",
        )

        self.assertEqual("probabilities", output)

    def test_loads_custom_head_with_metadata_policy_and_schema(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            model_dir = Path(directory)
            self._write_model(
                model_dir,
                "discogs-effnet-bs64-1",
                ["Electronic---Hardstyle"],
            )
            for spec in EssentiaMtgJamendoAnalyzer._DEFAULT_HEADS:
                self._write_model(model_dir, spec.model_name, ["existing"])
            self._write_model(
                model_dir,
                "modern-1",
                ["phonk"],
                tagging={
                    "topK": 3,
                    "minScore": 0.2,
                    "thresholds": {"phonk": 0.72},
                },
                input_name="embedding_input",
                output_name="genre_probabilities",
            )
            (model_dir / "custom-heads.json").write_text(
                json.dumps(
                    {
                        "heads": [
                            {
                                "namespace": "modern_genre",
                                "modelName": "modern-1",
                            }
                        ]
                    }
                ),
                encoding="utf-8",
            )

            analyzer = EssentiaMtgJamendoAnalyzer(
                model_dir,
                auto_download=False,
            )

        modern_head = next(
            head for head in analyzer._heads if head["namespace"] == "modern_genre"
        )
        self.assertEqual(3, modern_head["policy"].top_k)
        self.assertEqual(0.72, modern_head["policy"].threshold_for("phonk"))
        self.assertEqual(
            "embedding_input",
            modern_head["model"].parameters["input"],
        )
        self.assertEqual(
            "genre_probabilities",
            modern_head["model"].parameters["output"],
        )

    @staticmethod
    def _write_model(
        model_dir: Path,
        model_name: str,
        classes: list[str],
        tagging: dict | None = None,
        input_name: str = "model/Placeholder",
        output_name: str = "model/Sigmoid",
    ) -> None:
        (model_dir / f"{model_name}.pb").write_bytes(b"fake")
        metadata = {
            "classes": classes,
            "schema": {
                "inputs": [{"name": input_name}],
                "outputs": [
                    {
                        "name": output_name,
                        "output_purpose": "predictions",
                    }
                ],
            },
        }
        if tagging is not None:
            metadata["tagging"] = tagging
        (model_dir / f"{model_name}.json").write_text(
            json.dumps(metadata),
            encoding="utf-8",
        )


if __name__ == "__main__":
    unittest.main()
