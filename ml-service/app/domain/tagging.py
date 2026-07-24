from dataclasses import dataclass, field
import json
import math
from pathlib import Path
import re
from typing import Any, Mapping, Sequence


@dataclass(frozen=True)
class TaggingPolicy:
    top_k: int = 10
    min_score: float = 0.10
    thresholds: Mapping[str, float] = field(default_factory=dict)

    def __post_init__(self) -> None:
        if self.top_k <= 0:
            raise ValueError("Tagging top_k must be greater than zero.")
        _validate_probability(self.min_score, "Tagging min_score")
        for label, threshold in self.thresholds.items():
            if not str(label).strip():
                raise ValueError("Tagging threshold labels cannot be empty.")
            _validate_probability(float(threshold), f"Threshold for {label}")

    def threshold_for(self, label: str) -> float:
        return float(self.thresholds.get(label, self.min_score))

    def with_overrides(self, value: Mapping[str, Any] | None) -> "TaggingPolicy":
        if not value:
            return self

        thresholds = value.get("thresholds", self.thresholds)
        if not isinstance(thresholds, Mapping):
            raise ValueError("Tagging thresholds must be an object keyed by label.")

        return TaggingPolicy(
            top_k=int(value.get("topK", self.top_k)),
            min_score=float(value.get("minScore", self.min_score)),
            thresholds={str(label): float(score) for label, score in thresholds.items()},
        )


@dataclass(frozen=True)
class ClassificationHeadSpec:
    namespace: str
    model_name: str
    policy: TaggingPolicy = field(default_factory=TaggingPolicy)

    def __post_init__(self) -> None:
        if not self.namespace.strip():
            raise ValueError("Classification head namespace cannot be empty.")
        if len(self.namespace) > 50:
            raise ValueError("Classification head namespace cannot exceed 50 characters.")
        if not self.model_name.strip():
            raise ValueError("Classification head modelName cannot be empty.")
        if len(self.model_name) > 100:
            raise ValueError("Classification head modelName cannot exceed 100 characters.")

    @staticmethod
    def from_mapping(value: Mapping[str, Any]) -> "ClassificationHeadSpec":
        return ClassificationHeadSpec(
            namespace=str(value.get("namespace", "")).strip(),
            model_name=str(value.get("modelName", "")).strip(),
            policy=TaggingPolicy().with_overrides(value),
        )


@dataclass(frozen=True)
class RankedLabel:
    label: str
    score: float


@dataclass(frozen=True)
class DiscogsClass:
    namespace: str
    label: str
    category: str


def load_custom_head_specs(path: Path | None) -> list[ClassificationHeadSpec]:
    if path is None or not path.exists():
        return []

    with path.open("r", encoding="utf-8") as file:
        document = json.load(file)

    raw_heads = document.get("heads")
    if not isinstance(raw_heads, list):
        raise ValueError(f"Custom head manifest must contain a heads array: {path}")

    specs: list[ClassificationHeadSpec] = []
    for raw_head in raw_heads:
        if not isinstance(raw_head, Mapping):
            raise ValueError(f"Each custom head manifest entry must be an object: {path}")
        if raw_head.get("enabled", True):
            specs.append(ClassificationHeadSpec.from_mapping(raw_head))

    namespaces = [spec.namespace for spec in specs]
    if len(namespaces) != len(set(namespaces)):
        raise ValueError(f"Custom head namespaces must be unique: {path}")

    return specs


def select_ranked_labels(
    labels: Sequence[str],
    scores: Sequence[float],
    policy: TaggingPolicy,
) -> list[RankedLabel]:
    if len(labels) != len(scores):
        raise ValueError(
            f"Classifier returned {len(scores)} scores for {len(labels)} labels."
        )

    candidates: list[RankedLabel] = []
    for label, raw_score in zip(labels, scores):
        score = float(raw_score)
        if not math.isfinite(score) or score < policy.threshold_for(label):
            continue
        candidates.append(RankedLabel(label=label, score=score))

    candidates.sort(key=lambda item: item.score, reverse=True)
    return candidates[: policy.top_k]


def parse_discogs_class(value: str) -> DiscogsClass:
    raw_category, separator, raw_label = value.partition("---")
    category = raw_category.strip() if separator else "Style"
    label = raw_label.strip() if separator else raw_category.strip()
    if not label:
        raise ValueError(f"Discogs class has no label: {value!r}")

    namespace_suffix = _slug(category)
    return DiscogsClass(
        namespace=f"discogs.{namespace_suffix}",
        label=label,
        category=category,
    )


def _slug(value: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "_", value.strip().lower()).strip("_")
    return slug or "style"


def _validate_probability(value: float, name: str) -> None:
    if not math.isfinite(value) or value < 0 or value > 1:
        raise ValueError(f"{name} must be between 0 and 1.")
