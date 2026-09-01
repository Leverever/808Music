from __future__ import annotations

import itertools
import json
import math
import re
import time
from collections import defaultdict
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd
from PIL import Image, ImageDraw, ImageFont
from sklearn.cluster import AgglomerativeClustering, HDBSCAN, KMeans
from sklearn.decomposition import PCA
from sklearn.metrics import (
    adjusted_rand_score,
    calinski_harabasz_score,
    davies_bouldin_score,
    silhouette_score,
)
from sklearn.preprocessing import normalize


ROOT = Path(__file__).resolve().parent
DATA = ROOT / "data"
RESULTS = ROOT / "results"
FIGURES = ROOT / "figures"
RESULTS.mkdir(parents=True, exist_ok=True)
FIGURES.mkdir(parents=True, exist_ok=True)

TOP_K = 10
PROFILE_WINDOW_DAYS = 90
RECENCY_HALF_LIFE_DAYS = 14.0
MAX_RECENT_TRACKS = 50
MAX_TAG_AFFINITIES = 100
MAX_CLUSTER_AFFINITIES = 50

INTERACTION_WEIGHTS = {
    1: 0.5,   # PlayStarted
    2: 2.0,   # PlayCompleted
    3: -1.5,  # Skipped
    4: 4.0,   # Liked
    5: -3.0,  # Unliked
    6: 3.0,   # AddedToPlaylist
    7: -3.0,  # RemovedFromPlaylist
}


def read_csv(name: str) -> pd.DataFrame:
    return pd.read_csv(DATA / name, dtype=str, keep_default_na=True)


def number(series: pd.Series) -> pd.Series:
    return pd.to_numeric(series.astype("string").str.replace(",", ".", regex=False), errors="coerce")


def dates(series: pd.Series) -> pd.Series:
    return pd.to_datetime(series, dayfirst=True, format="mixed", errors="coerce")


def normalize_tag(value: str) -> str:
    return "".join(ch for ch in str(value).strip().lower() if ch.isalnum())


def clamp01(value: float) -> float:
    if not math.isfinite(value):
        return 0.0
    return min(1.0, max(0.0, value))


def average_available(*values: float) -> float:
    available = [value for value in values if value > 0]
    return clamp01(float(np.mean(available))) if available else 0.0


def load_inputs():
    catalog = read_csv("catalog.csv")
    catalog["TrackId"] = number(catalog["TrackId"]).astype(int)
    catalog["Streams"] = number(catalog["Streams"]).fillna(0).astype(int)
    catalog["AlbumId"] = number(catalog["AlbumId"])
    catalog["AlbumArtistId"] = number(catalog["AlbumArtistId"])

    valid_rows: list[int] = []
    vectors: list[list[float]] = []
    for index, value in catalog["EmbeddingJson"].items():
        if not isinstance(value, str) or not value.strip():
            continue
        try:
            vector = json.loads(value)
        except json.JSONDecodeError:
            continue
        if len(vector) != 1280:
            continue
        valid_rows.append(index)
        vectors.append(vector)

    analyzed = catalog.loc[valid_rows].copy().reset_index(drop=True)
    x = normalize(np.asarray(vectors, dtype=np.float32))
    track_ids = analyzed["TrackId"].astype(int).to_numpy()
    id_to_index = {int(track_id): index for index, track_id in enumerate(track_ids)}

    tags_df = read_csv("audio_tags.csv")
    tags_df["TrackId"] = number(tags_df["TrackId"]).astype(int)
    tags_df["Score"] = number(tags_df["Score"]).fillna(0.0)
    tags_df["NormalizedLabel"] = tags_df["Label"].map(normalize_tag)
    tags_by_track: dict[int, list[dict[str, object]]] = defaultdict(list)
    for row in tags_df.itertuples(index=False):
        if not row.NormalizedLabel:
            continue
        tags_by_track[int(row.TrackId)].append(
            {
                "namespace": str(row.Namespace),
                "label": str(row.Label),
                "normalized": str(row.NormalizedLabel),
                "score": float(row.Score),
            }
        )

    runs = read_csv("cluster_runs.csv")
    active_runs = runs[(runs["IsActive"].str.lower() == "true") & (runs["Status"] == "3")]
    active_run_id = str(active_runs.iloc[-1]["ClusterRunId"]) if len(active_runs) else ""

    assignments = read_csv("cluster_assignments.csv")
    assignments["TrackId"] = number(assignments["TrackId"]).astype(int)
    assignments["MembershipScore"] = number(assignments["MembershipScore"]).fillna(1.0)
    assignments["IsNoiseBool"] = assignments["IsNoise"].str.lower() == "true"
    active_assignments = assignments[
        (assignments["ClusterRunId"].str.lower() == active_run_id.lower())
        & (~assignments["IsNoiseBool"])
    ]
    cluster_by_track: dict[int, tuple[str, float]] = {}
    for row in active_assignments.itertuples(index=False):
        lookup_key = f"{str(row.ClusterRunId).replace('-', '').lower()}:{str(row.ClusterKey).strip().lower()}"
        cluster_by_track[int(row.TrackId)] = (lookup_key, float(row.MembershipScore))

    artist_tracks = read_csv("artist_tracks.csv")
    artist_tracks["TrackId"] = number(artist_tracks["TrackId"]).astype(int)
    artist_tracks["ArtistId"] = number(artist_tracks["ArtistId"]).astype(int)
    artists_by_track: dict[int, list[int]] = defaultdict(list)
    for row in artist_tracks.itertuples(index=False):
        if int(row.ArtistId) not in artists_by_track[int(row.TrackId)]:
            artists_by_track[int(row.TrackId)].append(int(row.ArtistId))

    interactions = read_csv("interactions.csv")
    interactions["UserId"] = number(interactions["UserId"]).astype(int)
    interactions["TrackId"] = number(interactions["TrackId"]).astype(int)
    interactions["InteractionType"] = number(interactions["InteractionType"]).astype(int)
    interactions["CompletionRatioNum"] = number(interactions["CompletionRatio"])
    interactions["OccurredAtDt"] = dates(interactions["OccurredAt"])
    interactions = interactions.sort_values(["UserId", "OccurredAtDt", "CreatedAt"]).reset_index(drop=True)

    return (
        catalog,
        analyzed,
        x,
        track_ids,
        id_to_index,
        tags_df,
        tags_by_track,
        active_run_id,
        cluster_by_track,
        artists_by_track,
        interactions,
    )


def top_tag_sets(tags_by_track: dict[int, list[dict[str, object]]], count: int = 5):
    result: dict[int, set[str]] = {}
    for track_id, tags in tags_by_track.items():
        ordered = sorted(tags, key=lambda tag: float(tag["score"]), reverse=True)
        result[track_id] = {str(tag["normalized"]) for tag in ordered[:count]}
    return result


def tag_coherence(labels: np.ndarray, track_ids: np.ndarray, tags_by_track) -> float:
    values: list[float] = []
    for cluster_label in sorted(set(labels.tolist())):
        if cluster_label == -1:
            continue
        member_ids = track_ids[labels == cluster_label]
        aggregate: dict[str, float] = defaultdict(float)
        for track_id in member_ids:
            for tag in tags_by_track.get(int(track_id), []):
                aggregate[str(tag["normalized"])] += float(tag["score"])
        cluster_top = {key for key, _ in sorted(aggregate.items(), key=lambda item: item[1], reverse=True)[:10]}
        if not cluster_top:
            continue
        for track_id in member_ids:
            track_top = sorted(
                tags_by_track.get(int(track_id), []),
                key=lambda tag: float(tag["score"]),
                reverse=True,
            )[:5]
            track_keys = {str(tag["normalized"]) for tag in track_top}
            if track_keys:
                values.append(len(track_keys & cluster_top) / len(track_keys))
    return float(np.mean(values)) if values else 0.0


def cluster_metrics(labels: np.ndarray, x: np.ndarray, track_ids: np.ndarray, tags_by_track):
    usable = labels != -1
    usable_labels = labels[usable]
    cluster_count = len(set(usable_labels.tolist()))
    if cluster_count < 2 or usable.sum() <= cluster_count:
        silhouette = float("nan")
        db = float("nan")
        ch = float("nan")
    else:
        silhouette = float(silhouette_score(x[usable], usable_labels, metric="cosine"))
        db = float(davies_bouldin_score(x[usable], usable_labels))
        ch = float(calinski_harabasz_score(x[usable], usable_labels))
    sizes = pd.Series(usable_labels).value_counts()
    return {
        "cluster_count": cluster_count,
        "noise_ratio": float(1.0 - usable.mean()),
        "silhouette_cosine": silhouette,
        "davies_bouldin": db,
        "calinski_harabasz": ch,
        "smallest_cluster": int(sizes.min()) if len(sizes) else 0,
        "largest_cluster": int(sizes.max()) if len(sizes) else 0,
        "tag_coherence_at_10": tag_coherence(labels, track_ids, tags_by_track),
    }


def fit_clustering(algorithm: str, parameter: int, x: np.ndarray, random_state: int = 42):
    if algorithm == "kmeans":
        return KMeans(n_clusters=parameter, random_state=random_state, n_init="auto").fit_predict(x)
    if algorithm == "agglomerative":
        return AgglomerativeClustering(n_clusters=parameter, linkage="ward").fit_predict(x)
    if algorithm == "hdbscan":
        return HDBSCAN(
            min_cluster_size=parameter,
            min_samples=None,
            metric="euclidean",
            copy=True,
        ).fit_predict(x)
    raise ValueError(algorithm)


def clustering_stability(algorithm: str, parameter: int, x: np.ndarray, baseline: np.ndarray) -> float:
    comparisons: list[float] = []
    if algorithm == "kmeans":
        for seed in (7, 42, 808, 2026, 7777):
            labels = fit_clustering(algorithm, parameter, x, seed)
            comparisons.append(adjusted_rand_score(baseline, labels))
    else:
        rng = np.random.default_rng(808)
        for _ in range(5):
            perturbed = normalize(x + rng.normal(0.0, 0.001, size=x.shape))
            labels = fit_clustering(algorithm, parameter, perturbed)
            comparisons.append(adjusted_rand_score(baseline, labels))
    return float(np.mean(comparisons))


def evaluate_clustering(x, track_ids, tags_by_track):
    rows: list[dict[str, object]] = []
    label_store: dict[str, np.ndarray] = {}
    for algorithm, parameters in (
        ("kmeans", (8, 12, 16)),
        ("agglomerative", (8, 12, 16)),
        ("hdbscan", (5, 10, 15)),
    ):
        for parameter in parameters:
            started = time.perf_counter()
            labels = fit_clustering(algorithm, parameter, x)
            fit_seconds = time.perf_counter() - started
            config = f"{algorithm}:{parameter}"
            label_store[config] = labels
            row = {
                "algorithm": algorithm,
                "parameter": parameter,
                **cluster_metrics(labels, x, track_ids, tags_by_track),
                "stability_ari": clustering_stability(algorithm, parameter, x, labels),
                "fit_time_seconds": fit_seconds,
            }
            rows.append(row)

    frame = pd.DataFrame(rows)
    frame["selection_score"] = (
        frame["silhouette_cosine"].fillna(-1.0) * 0.50
        + frame["tag_coherence_at_10"] * 0.25
        + frame["stability_ari"] * 0.25
        - frame["noise_ratio"] * 0.25
    )
    frame = frame.sort_values("selection_score", ascending=False).reset_index(drop=True)
    frame.to_csv(RESULTS / "clustering_metrics.csv", index=False)

    best = frame.iloc[0]
    best_key = f"{best['algorithm']}:{int(best['parameter'])}"
    labels = label_store[best_key]
    pd.DataFrame({"TrackId": track_ids, "Cluster": labels}).to_csv(
        RESULTS / "selected_cluster_assignments.csv", index=False
    )
    return frame, best_key, labels


def build_profile(
    history: pd.DataFrame,
    cutoff: pd.Timestamp,
    id_to_index,
    x,
    tags_by_track,
    cluster_by_track,
    use_recency_decay: bool = True,
):
    if history.empty:
        return {
            "embedding": np.array([], dtype=float),
            "tags": {},
            "clusters": {},
            "recent": set(),
            "positive_track_count": 0,
            "popularity_counts": {},
            "seed_embedding": np.array([], dtype=float),
        }
    window_start = cutoff - pd.Timedelta(days=PROFILE_WINDOW_DAYS)
    history = history[(history["OccurredAtDt"] >= window_start) & (history["OccurredAtDt"] < cutoff)].copy()
    weighted: dict[int, float] = defaultdict(float)
    for row in history.itertuples(index=False):
        base = INTERACTION_WEIGHTS.get(int(row.InteractionType), 0.0)
        if base == 0:
            continue
        age_days = max(0.0, (cutoff - row.OccurredAtDt).total_seconds() / 86400.0)
        recency = 0.5 ** (age_days / RECENCY_HALF_LIFE_DAYS) if use_recency_decay else 1.0
        weighted[int(row.TrackId)] += base * recency

    positive = {track_id: weight for track_id, weight in weighted.items() if weight > 0 and track_id in id_to_index}
    if positive:
        positive_ids = list(positive)
        weights = np.asarray([positive[track_id] for track_id in positive_ids], dtype=float)
        vectors = np.vstack([x[id_to_index[track_id]] for track_id in positive_ids])
        embedding = np.average(vectors, axis=0, weights=weights)
    else:
        embedding = np.array([], dtype=float)

    tag_affinities: dict[str, float] = defaultdict(float)
    for track_id, track_weight in weighted.items():
        for tag in tags_by_track.get(track_id, []):
            tag_affinities[str(tag["normalized"])] += track_weight * float(tag["score"])
    tag_affinities = dict(
        sorted(tag_affinities.items(), key=lambda item: abs(item[1]), reverse=True)[:MAX_TAG_AFFINITIES]
    )

    cluster_affinities: dict[str, float] = defaultdict(float)
    for track_id, track_weight in weighted.items():
        if track_id in cluster_by_track:
            key, membership = cluster_by_track[track_id]
            cluster_affinities[key] += track_weight * membership
    cluster_affinities = dict(
        sorted(cluster_affinities.items(), key=lambda item: abs(item[1]), reverse=True)[:MAX_CLUSTER_AFFINITIES]
    )

    recent = []
    for track_id in history.sort_values("OccurredAtDt", ascending=False)["TrackId"].astype(int):
        if track_id not in recent:
            recent.append(track_id)
        if len(recent) >= MAX_RECENT_TRACKS:
            break
    popularity_counts = (
        history[history["InteractionType"] == 1]
        .groupby("TrackId")
        .size()
        .astype(int)
        .to_dict()
    )
    seed_embedding = np.array([], dtype=float)
    recent_starts = history[history["InteractionType"] == 1].sort_values("OccurredAtDt", ascending=False)
    for track_id in recent_starts["TrackId"].astype(int):
        if track_id in id_to_index:
            seed_embedding = x[id_to_index[track_id]]
            break
    return {
        "embedding": embedding,
        "tags": tag_affinities,
        "clusters": cluster_affinities,
        "recent": set(recent),
        "positive_track_count": len(positive),
        "popularity_counts": popularity_counts,
        "seed_embedding": seed_embedding,
    }


def embedding_similarity(candidate: np.ndarray, reference: np.ndarray) -> float:
    if candidate.size == 0 or reference.size == 0 or candidate.shape != reference.shape:
        return 0.0
    return clamp01(float(np.dot(candidate, reference) / (np.linalg.norm(candidate) * np.linalg.norm(reference))))


def tag_affinity_score(track_id: int, affinities: dict[str, float], tags_by_track) -> float:
    positive_total = sum(value for value in affinities.values() if value > 0)
    if positive_total <= 0:
        return 0.0
    score = sum(
        float(tag["score"]) * affinities.get(str(tag["normalized"]), 0.0)
        for tag in tags_by_track.get(track_id, [])
    )
    return clamp01(score / positive_total)


def cluster_affinity_score(track_id: int, affinities: dict[str, float], cluster_by_track) -> float:
    positive_total = sum(value for value in affinities.values() if value > 0)
    if positive_total <= 0 or track_id not in cluster_by_track:
        return 0.0
    key, membership = cluster_by_track[track_id]
    return clamp01(affinities.get(key, 0.0) * membership / positive_total)


def score_general_candidate(
    track_id,
    profile,
    analyzed,
    id_to_index,
    x,
    tags_by_track,
    cluster_by_track,
    variant="hybrid",
):
    candidate_embedding = x[id_to_index[track_id]] if track_id in id_to_index else np.array([], dtype=float)
    embedding_score = embedding_similarity(candidate_embedding, profile["embedding"])
    tag_score = tag_affinity_score(track_id, profile["tags"], tags_by_track)
    cluster_score = cluster_affinity_score(track_id, profile["clusters"], cluster_by_track)

    if variant == "no_embedding":
        embedding_score = 0.0
    if variant == "no_tags":
        tag_score = 0.0
    if variant == "no_clusters":
        cluster_score = 0.0

    user_profile = average_available(embedding_score, tag_score, cluster_score)
    tag_cluster = average_available(tag_score, cluster_score)
    row = analyzed.iloc[id_to_index[track_id]]
    max_training_popularity = max([1, *profile["popularity_counts"].values()])
    max_track_id = max(1, int(analyzed["TrackId"].max()))
    popularity = math.log1p(profile["popularity_counts"].get(track_id, 0)) / math.log1p(max_training_popularity)
    freshness = track_id / max_track_id
    freshness_popularity = clamp01(popularity * 0.70 + freshness * 0.30)
    novelty = 0.2 if track_id in profile["recent"] else 1.0
    if variant == "no_novelty":
        novelty = 1.0

    if variant == "pop":
        return popularity, popularity
    elif variant == "fresh_pop":
        return freshness_popularity, freshness_popularity
    elif variant == "content_seed":
        score = embedding_similarity(candidate_embedding, profile["seed_embedding"])
    elif variant == "embedding_only":
        score = embedding_score
    elif variant == "tag_cluster_only":
        score = tag_cluster
    else:
        score = user_profile * 0.45 + tag_cluster * 0.30 + freshness_popularity * 0.15 + novelty * 0.10
    if score <= 0.000001:
        score = 0.05 + freshness_popularity * 0.2
    if variant not in {"content_seed", "embedding_only", "tag_cluster_only"}:
        score *= novelty
    return score, freshness_popularity


def apply_diversity(ranked, analyzed, artists_by_track, recent, limit=TOP_K, enforce=True):
    if not enforce:
        return [track_id for track_id, *_ in ranked[:limit]]
    selected: list[int] = []
    artist_counts: dict[int, int] = defaultdict(int)
    album_counts: dict[int, int] = defaultdict(int)
    album_by_track = {
        int(row.TrackId): (None if pd.isna(row.AlbumId) else int(float(row.AlbumId)))
        for row in analyzed.itertuples(index=False)
    }

    for skip_recent, enforce_caps in ((True, True), (False, True), (False, False)):
        for track_id, *_ in ranked:
            if track_id in selected:
                continue
            if skip_recent and track_id in recent:
                continue
            artists = artists_by_track.get(track_id, [])
            album_id = album_by_track.get(track_id)
            if enforce_caps:
                if any(artist_counts[artist_id] >= 4 for artist_id in artists):
                    continue
                if album_id is not None and album_counts[album_id] >= 3:
                    continue
            selected.append(track_id)
            for artist_id in artists:
                artist_counts[artist_id] += 1
            if album_id is not None:
                album_counts[album_id] += 1
            if len(selected) >= limit:
                return selected
    return selected


def rank_general(
    profile,
    analyzed,
    id_to_index,
    x,
    tags_by_track,
    cluster_by_track,
    artists_by_track,
    variant,
    limit=20,
):
    ranked = []
    for track_id in analyzed["TrackId"].astype(int):
        score, fallback = score_general_candidate(
            track_id,
            profile,
            analyzed,
            id_to_index,
            x,
            tags_by_track,
            cluster_by_track,
            variant,
        )
        ranked.append((track_id, score, fallback))
    ranked.sort(key=lambda item: (-item[1], -item[2], item[0]))
    use_diversity = variant in {
        "hybrid",
        "no_embedding",
        "no_tags",
        "no_clusters",
        "no_decay",
        "no_novelty",
    }
    selected = apply_diversity(ranked, analyzed, artists_by_track, profile["recent"], limit, use_diversity)
    full_order = [item[0] for item in ranked]
    return selected, full_order


def intra_list_diversity(track_list: list[int], id_to_index, x) -> float:
    vectors = [x[id_to_index[track_id]] for track_id in track_list if track_id in id_to_index]
    if len(vectors) < 2:
        return 0.0
    similarities = [float(np.dot(left, right)) for left, right in itertools.combinations(vectors, 2)]
    return float(1.0 - np.mean(similarities))


def evaluate_recommendations(analyzed, x, id_to_index, tags_by_track, cluster_by_track, artists_by_track, interactions):
    variants = (
        "pop",
        "fresh_pop",
        "content_seed",
        "embedding_only",
        "tag_cluster_only",
        "hybrid",
        "no_embedding",
        "no_tags",
        "no_clusters",
        "no_decay",
        "no_novelty",
        "no_diversity",
    )
    episode_rows: list[dict[str, object]] = []
    positive_mask = (
        interactions["InteractionType"].isin([4, 6])
        | ((interactions["InteractionType"] == 2) & (interactions["CompletionRatioNum"] >= 0.9))
    )
    positives = interactions[positive_mask].copy()
    evaluation_events: list[dict[str, object]] = []
    for positive in positives.itertuples(index=False):
        label_time = positive.OccurredAtDt
        cutoff = label_time
        # A completed play is evidence that the choice made at PlayStarted was
        # positive.  Using the completion timestamp would leak the current
        # PlayStarted event into RecentTrackIds and unfairly suppress the target.
        candidate_starts = interactions[
            (interactions["UserId"] == int(positive.UserId))
            & (interactions["TrackId"] == int(positive.TrackId))
            & (interactions["InteractionType"] == 1)
            & (interactions["OccurredAtDt"] < label_time)
            & (interactions["OccurredAtDt"] >= label_time - pd.Timedelta(minutes=30))
        ]
        if len(candidate_starts):
            cutoff = candidate_starts.iloc[-1]["OccurredAtDt"]
        evaluation_events.append(
            {
                "UserId": int(positive.UserId),
                "TrackId": int(positive.TrackId),
                "Cutoff": cutoff,
                "LabelAt": label_time,
                "InteractionType": int(positive.InteractionType),
            }
        )

    for event in evaluation_events:
        cutoff = event["Cutoff"]
        user_history = interactions[
            (interactions["UserId"] == int(event["UserId"]))
            & (interactions["OccurredAtDt"] < cutoff)
        ]
        if len(user_history) < 10 or int(event["TrackId"]) not in id_to_index:
            continue
        profile = build_profile(user_history, cutoff, id_to_index, x, tags_by_track, cluster_by_track)
        profile_without_decay = build_profile(
            user_history,
            cutoff,
            id_to_index,
            x,
            tags_by_track,
            cluster_by_track,
            use_recency_decay=False,
        )
        for variant in variants:
            active_profile = profile_without_decay if variant == "no_decay" else profile
            top, full_order = rank_general(
                active_profile,
                analyzed,
                id_to_index,
                x,
                tags_by_track,
                cluster_by_track,
                artists_by_track,
                variant,
            )
            target = int(event["TrackId"])
            rank = full_order.index(target) + 1 if target in full_order else None
            top_rank = top.index(target) + 1 if target in top else None
            metrics_by_k = {}
            for k in (5, 10, 20):
                rank_at_k = top_rank if top_rank is not None and top_rank <= k else None
                hit = 1 if rank_at_k is not None else 0
                metrics_by_k.update(
                    {
                        f"PrecisionAt{k}": hit / k,
                        f"RecallAt{k}": hit,
                        f"NdcgAt{k}": (1.0 / math.log2(rank_at_k + 1)) if rank_at_k else 0.0,
                    }
                )
            episode_rows.append(
                {
                    "UserId": int(event["UserId"]),
                    "Cutoff": cutoff.isoformat(),
                    "LabelAt": event["LabelAt"].isoformat(),
                    "PositiveInteractionType": int(event["InteractionType"]),
                    "TargetTrackId": target,
                    "Variant": variant,
                    "HistoryEvents": len(user_history),
                    "ProfilePositiveTracks": active_profile["positive_track_count"],
                    "Rank": rank,
                    "TopKRankAfterDiversity": top_rank,
                    **metrics_by_k,
                    "ReciprocalRank": (1.0 / rank) if rank else 0.0,
                    "IntraListDiversity": intra_list_diversity(top[:10], id_to_index, x),
                    "RecommendedTrackIds": "|".join(map(str, top[:10])),
                    "RecommendedTrackIdsAt20": "|".join(map(str, top[:20])),
                }
            )

    episodes = pd.DataFrame(episode_rows)
    episodes.to_csv(RESULTS / "recommendation_episodes.csv", index=False)
    metric_rows = []
    catalog_size = len(analyzed)
    for variant, group in episodes.groupby("Variant", sort=False):
        recommended = set()
        for value in group["RecommendedTrackIds"]:
            recommended.update(int(item) for item in str(value).split("|") if item)
        metric_rows.append(
            {
                "variant": variant,
                "episodes": len(group),
                "precision_at_5": float(group["PrecisionAt5"].mean()),
                "recall_at_5": float(group["RecallAt5"].mean()),
                "ndcg_at_5": float(group["NdcgAt5"].mean()),
                "precision_at_10": float(group["PrecisionAt10"].mean()),
                "recall_at_10": float(group["RecallAt10"].mean()),
                "ndcg_at_10": float(group["NdcgAt10"].mean()),
                "precision_at_20": float(group["PrecisionAt20"].mean()),
                "recall_at_20": float(group["RecallAt20"].mean()),
                "ndcg_at_20": float(group["NdcgAt20"].mean()),
                "mrr": float(group["ReciprocalRank"].mean()),
                "catalog_coverage_at_10": len(recommended) / catalog_size,
                "intra_list_diversity": float(group["IntraListDiversity"].mean()),
            }
        )
    metrics = pd.DataFrame(metric_rows)
    ordered = {name: index for index, name in enumerate(variants)}
    metrics["order"] = metrics["variant"].map(ordered)
    metrics = metrics.sort_values("order").drop(columns="order")
    metrics.to_csv(RESULTS / "recommendation_metrics.csv", index=False)
    return episodes, metrics, len(positives)


def seed_context(seed_id, id_to_index, x, tags_by_track, cluster_by_track):
    tag_scores: dict[str, float] = defaultdict(float)
    for tag in tags_by_track.get(seed_id, []):
        tag_scores[str(tag["normalized"])] += float(tag["score"])
    cluster_keys = {cluster_by_track[seed_id][0]} if seed_id in cluster_by_track else set()
    return x[id_to_index[seed_id]], dict(tag_scores), cluster_keys


def rank_radio(seed_id, analyzed, id_to_index, x, tags_by_track, cluster_by_track, artists_by_track):
    seed_embedding, seed_tags, seed_clusters = seed_context(seed_id, id_to_index, x, tags_by_track, cluster_by_track)
    denominator = sum(seed_tags.values())
    ranked = []
    for track_id in analyzed["TrackId"].astype(int):
        if track_id == seed_id:
            continue
        embedding_score = embedding_similarity(x[id_to_index[track_id]], seed_embedding)
        shared = 0.0
        if denominator > 0:
            shared = clamp01(
                sum(
                    float(tag["score"]) * seed_tags.get(str(tag["normalized"]), 0.0)
                    for tag in tags_by_track.get(track_id, [])
                )
                / denominator
            )
        cluster_match = 1.0 if track_id in cluster_by_track and cluster_by_track[track_id][0] in seed_clusters else 0.0
        seed_similarity = average_available(embedding_score, shared, cluster_match)
        score = seed_similarity * 0.50 + shared * 0.25 + cluster_match * 0.15
        ranked.append((track_id, score, score))
    ranked.sort(key=lambda item: (-item[1], item[0]))
    return apply_diversity(ranked, analyzed, artists_by_track, set(), TOP_K, True)


def evaluate_cold_start(analyzed, x, id_to_index, tags_by_track, cluster_by_track, artists_by_track, interactions):
    interacted_ids = set(interactions["TrackId"].astype(int))
    cold_items = analyzed[
        (~analyzed["TrackId"].isin(interacted_ids))
        & (analyzed["Streams"].astype(int) == 0)
    ]["TrackId"].astype(int).tolist()
    seeds: list[int] = []
    seen_clusters: set[str] = set()
    for track_id in cold_items:
        cluster = cluster_by_track.get(track_id, ("", 0.0))[0]
        if cluster and cluster not in seen_clusters:
            seeds.append(track_id)
            seen_clusters.add(cluster)
        if len(seeds) >= 12:
            break
    for track_id in cold_items:
        if len(seeds) >= 12:
            break
        if track_id not in seeds:
            seeds.append(track_id)

    top_sets = top_tag_sets(tags_by_track)
    rows = []
    for seed_id in seeds:
        recommendations = rank_radio(
            seed_id,
            analyzed,
            id_to_index,
            x,
            tags_by_track,
            cluster_by_track,
            artists_by_track,
        )
        seed_cluster = cluster_by_track.get(seed_id, ("", 0.0))[0]
        seed_tags = top_sets.get(seed_id, set())
        same_cluster = []
        overlaps = []
        similarities = []
        for track_id in recommendations:
            same_cluster.append(1.0 if cluster_by_track.get(track_id, ("", 0.0))[0] == seed_cluster else 0.0)
            candidate_tags = top_sets.get(track_id, set())
            union = seed_tags | candidate_tags
            overlaps.append(len(seed_tags & candidate_tags) / len(union) if union else 0.0)
            similarities.append(float(np.dot(x[id_to_index[seed_id]], x[id_to_index[track_id]])))
        rows.append(
            {
                "SeedTrackId": seed_id,
                "Recommendations": len(recommendations),
                "SameClusterAt10": float(np.mean(same_cluster)) if same_cluster else 0.0,
                "TagJaccardAt10": float(np.mean(overlaps)) if overlaps else 0.0,
                "EmbeddingSimilarityAt10": float(np.mean(similarities)) if similarities else 0.0,
                "RecommendedTrackIds": "|".join(map(str, recommendations)),
            }
        )
    frame = pd.DataFrame(rows)
    frame.to_csv(RESULTS / "cold_start_item_cases.csv", index=False)
    return frame, len(cold_items)


def evaluate_operational(catalog):
    analyses = read_csv("audio_analyses.csv")
    analyses["StatusNum"] = number(analyses["Status"]).astype(int)
    analyses["StartedAtDt"] = dates(analyses["StartedAt"])
    analyses["CompletedAtDt"] = dates(analyses["CompletedAt"])
    analyses["DurationSeconds"] = (analyses["CompletedAtDt"] - analyses["StartedAtDt"]).dt.total_seconds()

    stem_sets = read_csv("stem_sets.csv")
    stem_sets["StatusNum"] = number(stem_sets["Status"]).astype(int)
    stem_sets["StartedAtDt"] = dates(stem_sets["StartedAt"])
    stem_sets["CompletedAtDt"] = dates(stem_sets["CompletedAt"])
    stem_sets["DurationSeconds"] = (stem_sets["CompletedAtDt"] - stem_sets["StartedAtDt"]).dt.total_seconds()
    stems = read_csv("stems.csv")
    stems["SizeBytesNum"] = number(stems["SizeBytes"])
    stems["DurationMsNum"] = number(stems["DurationMs"])
    stems["SampleRateNum"] = number(stems["SampleRate"])
    stems["ChannelsNum"] = number(stems["Channels"])

    ready_stem_sets = stem_sets[stem_sets["StatusNum"] == 3]
    stem_counts = stems.groupby("StemSetId")["StemId"].count()
    ready_counts = ready_stem_sets[["StemSetId"]].merge(stem_counts.rename("StemCount"), left_on="StemSetId", right_index=True, how="left")
    complete_four = int((ready_counts["StemCount"].fillna(0) == 4).sum())

    summary = {
        "catalog_tracks": int(len(catalog)),
        "tracks_with_active_analysis": int(catalog["AnalysisId"].notna().sum()),
        "analysis_coverage": float(catalog["AnalysisId"].notna().mean()),
        "analysis_jobs": int(len(analyses)),
        "analysis_ready": int((analyses["StatusNum"] == 3).sum()),
        "analysis_failed": int((analyses["StatusNum"] == 4).sum()),
        "analysis_success_rate_terminal": float(
            (analyses["StatusNum"] == 3).sum()
            / max(1, analyses["StatusNum"].isin([3, 4]).sum())
        ),
        "analysis_latency_median_seconds": float(analyses["DurationSeconds"].dropna().median()),
        "analysis_latency_p95_seconds": float(analyses["DurationSeconds"].dropna().quantile(0.95)),
        "stem_jobs": int(len(stem_sets)),
        "stem_ready": int((stem_sets["StatusNum"] == 3).sum()),
        "stem_failed": int((stem_sets["StatusNum"] == 4).sum()),
        "stem_success_rate_terminal": float(
            (stem_sets["StatusNum"] == 3).sum()
            / max(1, stem_sets["StatusNum"].isin([3, 4]).sum())
        ),
        "stem_latency_median_seconds": float(stem_sets["DurationSeconds"].dropna().median()),
        "stem_latency_p95_seconds": float(stem_sets["DurationSeconds"].dropna().quantile(0.95)),
        "ready_stem_sets_with_exactly_four_stems": complete_four,
        "ready_stem_set_completeness": float(complete_four / max(1, len(ready_stem_sets))),
        "stem_objects": int(len(stems)),
        "stem_objects_with_metadata": int(
            ((stems["SizeBytesNum"] > 0) & (stems["DurationMsNum"] > 0) & (stems["SampleRateNum"] > 0)).sum()
        ),
    }
    (RESULTS / "operational_summary.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    analyses.drop(columns=["StartedAtDt", "CompletedAtDt"]).to_csv(RESULTS / "analysis_jobs_with_latency.csv", index=False)
    stem_sets.drop(columns=["StartedAtDt", "CompletedAtDt"]).to_csv(RESULTS / "stem_jobs_with_latency.csv", index=False)
    return summary, stem_sets


def font(size: int, bold: bool = False):
    candidates = [
        Path("C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf"),
        Path("C:/Windows/Fonts/calibrib.ttf" if bold else "C:/Windows/Fonts/calibri.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()


def bar_chart(path: Path, title: str, labels: list[str], series: list[tuple[str, list[float], str]], y_max=None):
    width, height = 1500, 850
    margin_left, margin_right, margin_top, margin_bottom = 140, 70, 130, 180
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    draw.text((width / 2, 38), title, fill="#111827", font=font(32, True), anchor="ma")
    plot_left, plot_top = margin_left, margin_top
    plot_right, plot_bottom = width - margin_right, height - margin_bottom
    all_values = [value for _, values, _ in series for value in values]
    maximum = y_max if y_max is not None else max(all_values + [1e-9]) * 1.12
    for tick in range(6):
        value = maximum * tick / 5
        y = plot_bottom - (plot_bottom - plot_top) * tick / 5
        draw.line((plot_left, y, plot_right, y), fill="#e5e7eb", width=2)
        draw.text((plot_left - 16, y), f"{value:.2f}", fill="#4b5563", font=font(19), anchor="rm")
    draw.line((plot_left, plot_top, plot_left, plot_bottom), fill="#374151", width=3)
    draw.line((plot_left, plot_bottom, plot_right, plot_bottom), fill="#374151", width=3)
    group_width = (plot_right - plot_left) / max(1, len(labels))
    bar_width = min(58, group_width * 0.75 / max(1, len(series)))
    for index, label in enumerate(labels):
        center = plot_left + group_width * (index + 0.5)
        for series_index, (series_name, values, color) in enumerate(series):
            value = float(values[index])
            x0 = center + (series_index - (len(series) - 1) / 2) * bar_width - bar_width * 0.42
            x1 = x0 + bar_width * 0.84
            y = plot_bottom - (plot_bottom - plot_top) * value / maximum
            draw.rectangle((x0, y, x1, plot_bottom), fill=color)
            draw.text(((x0 + x1) / 2, y - 7), f"{value:.2f}", fill="#111827", font=font(15), anchor="mb")
        wrapped = "\n".join(re.findall(r".{1,13}(?:\s+|$)", label) or [label])
        draw.multiline_text((center, plot_bottom + 20), wrapped.strip(), fill="#374151", font=font(17), anchor="ma", align="center")
    legend_x = plot_left
    legend_y = height - 48
    for series_name, _, color in series:
        draw.rectangle((legend_x, legend_y - 12, legend_x + 24, legend_y + 12), fill=color)
        draw.text((legend_x + 34, legend_y), series_name, fill="#1f2937", font=font(18), anchor="lm")
        legend_x += 34 + draw.textlength(series_name, font=font(18)) + 45
    image.save(path, dpi=(160, 160))


def scatter_chart(path: Path, title: str, points: np.ndarray, labels: np.ndarray):
    width, height = 1400, 900
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    draw.text((width / 2, 38), title, fill="#111827", font=font(31, True), anchor="ma")
    left, top, right, bottom = 105, 120, width - 70, height - 95
    draw.rectangle((left, top, right, bottom), outline="#6b7280", width=2)
    x_min, y_min = points.min(axis=0)
    x_max, y_max = points.max(axis=0)
    palette = [
        "#2563eb", "#dc2626", "#059669", "#d97706", "#7c3aed", "#0891b2",
        "#db2777", "#4d7c0f", "#9333ea", "#0f766e", "#b45309", "#475569",
        "#1d4ed8", "#be123c", "#15803d", "#a16207", "#6d28d9", "#0e7490",
    ]
    for point, label in zip(points, labels):
        x_coord = left + 20 + (right - left - 40) * (point[0] - x_min) / max(1e-9, x_max - x_min)
        y_coord = bottom - 20 - (bottom - top - 40) * (point[1] - y_min) / max(1e-9, y_max - y_min)
        color = "#9ca3af" if int(label) == -1 else palette[int(label) % len(palette)]
        draw.ellipse((x_coord - 5, y_coord - 5, x_coord + 5, y_coord + 5), fill=color, outline="white")
    draw.text((width / 2, bottom + 48), "Prva glavna komponenta", fill="#374151", font=font(20), anchor="ma")
    draw.text((left + 12, top + 12), "Druga glavna komponenta", fill="#374151", font=font(18), anchor="la")
    image.save(path, dpi=(160, 160))


def make_figures(clustering, selected_key, labels, x, recommendation_metrics, cold_start, stem_sets, operational):
    clustering_plot = clustering.sort_values(["algorithm", "parameter"])
    clustering_labels = [f"{row.algorithm}\n{int(row.parameter)}" for row in clustering_plot.itertuples(index=False)]
    bar_chart(
        FIGURES / "clustering_comparison.png",
        "Usporedba kvalitete klasteriranja audio-vektora",
        clustering_labels,
        [
            ("Silhouette (kosinus)", clustering_plot["silhouette_cosine"].fillna(0).tolist(), "#2563eb"),
            ("Semantička koherentnost", clustering_plot["tag_coherence_at_10"].tolist(), "#059669"),
            ("Stabilnost ARI", clustering_plot["stability_ari"].tolist(), "#d97706"),
        ],
        y_max=1.0,
    )
    pca = PCA(n_components=2, random_state=42).fit_transform(x)
    scatter_chart(
        FIGURES / "selected_clusters_pca.png",
        f"PCA prikaz odabranog rješenja ({selected_key.upper()})",
        pca,
        labels,
    )

    selected_variants = recommendation_metrics[
        recommendation_metrics["variant"].isin(["pop", "fresh_pop", "content_seed", "embedding_only", "tag_cluster_only", "hybrid"])
    ]
    bar_chart(
        FIGURES / "recommendation_comparison.png",
        "Eksplorativna usporedba pristupa preporučivanju (K=10)",
        ["POP", "Svježe + POP", "Sadržaj: seed", "Audio-profil", "Oznake + klaster", "Hibrid"],
        [
            ("Recall@10", selected_variants["recall_at_10"].tolist(), "#2563eb"),
            ("NDCG@10", selected_variants["ndcg_at_10"].tolist(), "#059669"),
            ("Pokrivenost kataloga", selected_variants["catalog_coverage_at_10"].tolist(), "#d97706"),
        ],
        y_max=1.0,
    )
    ablation = recommendation_metrics[
        recommendation_metrics["variant"].isin(["hybrid", "no_embedding", "no_tags", "no_clusters", "no_decay", "no_novelty", "no_diversity"])
    ]
    bar_chart(
        FIGURES / "recommendation_ablation.png",
        "Ablacijska analiza hibridnog sustava preporuka",
        ablation["variant"].tolist(),
        [
            ("Recall@10", ablation["recall_at_10"].tolist(), "#2563eb"),
            ("NDCG@10", ablation["ndcg_at_10"].tolist(), "#059669"),
            ("Raznolikost liste", ablation["intra_list_diversity"].tolist(), "#7c3aed"),
        ],
        y_max=1.0,
    )
    cold_means = cold_start[["SameClusterAt10", "TagJaccardAt10", "EmbeddingSimilarityAt10"]].mean()
    bar_chart(
        FIGURES / "cold_start_item.png",
        "Kvaliteta susjedstva za nove pjesme bez interakcija",
        ["Nove pjesme"],
        [
            ("Isti klaster@10", [float(cold_means["SameClusterAt10"])], "#2563eb"),
            ("Tag Jaccard@10", [float(cold_means["TagJaccardAt10"])], "#059669"),
            ("Sličnost vektora@10", [float(cold_means["EmbeddingSimilarityAt10"])], "#d97706"),
        ],
        y_max=1.0,
    )

    valid_latencies = stem_sets["DurationSeconds"].dropna()
    if len(valid_latencies):
        latency_values = [
            float(valid_latencies.median()),
            float(valid_latencies.quantile(0.90)),
            float(valid_latencies.quantile(0.95)),
        ]
        bar_chart(
            FIGURES / "stem_latency.png",
            "Vrijeme obrade Demucs poslova",
            ["Medijan", "P90", "P95"],
            [("Sekunde", latency_values, "#7c3aed")],
        )
    bar_chart(
        FIGURES / "pipeline_coverage.png",
        "Pokrivenost kataloga ML obradom",
        ["Audio-analiza", "Aktivni stem setovi"],
        [
            (
                "Udio kataloga",
                [
                    float(operational["analysis_coverage"]),
                    float((stem_sets["IsActive"].str.lower() == "true").sum() / operational["catalog_tracks"]),
                ],
                "#2563eb",
            )
        ],
        y_max=1.0,
    )


def write_summary(
    catalog,
    x,
    tags_df,
    clustering,
    selected_key,
    episodes,
    recommendation_metrics,
    positive_count,
    cold_start,
    cold_item_count,
    operational,
):
    hybrid = recommendation_metrics[recommendation_metrics["variant"] == "hybrid"].iloc[0]
    best = clustering.iloc[0]
    cold_means = cold_start[["SameClusterAt10", "TagJaccardAt10", "EmbeddingSimilarityAt10"]].mean()
    lines = [
        "# Sažetak izmjerenih rezultata",
        "",
        "## Skup podataka",
        "",
        f"- Pjesme u katalogu: {len(catalog)}",
        f"- Pjesme s valjanim aktivnim vektorom značajki: {len(x)} ({len(x) / len(catalog):.1%})",
        f"- Dimenzionalnost vektora: {x.shape[1]}",
        f"- Aktivne audio-oznake: {len(tags_df)}",
        f"- Korisnici s interakcijama: {episodes['UserId'].nunique() if len(episodes) else 0}",
        f"- Pozitivni događaji prema unaprijed zadanom pravilu: {positive_count}",
        f"- Valjane vremenske epizode za evaluaciju: {episodes[['UserId', 'Cutoff', 'TargetTrackId']].drop_duplicates().shape[0]}",
        "",
        "## Klasteriranje",
        "",
        f"Najbolji složeni rezultat ostvarila je konfiguracija `{selected_key}`. "
        f"Silhouette iznosi {best['silhouette_cosine']:.3f}, semantička koherentnost "
        f"{best['tag_coherence_at_10']:.3f}, stabilnost ARI {best['stability_ari']:.3f}, "
        f"a udio šuma {best['noise_ratio']:.1%}.",
        "",
        "## Sustav preporuka",
        "",
        f"Za hibridnu varijantu na {int(hybrid['episodes'])} vremenskih epizoda dobiveni su "
        f"Recall@10={hybrid['recall_at_10']:.3f}, NDCG@10={hybrid['ndcg_at_10']:.3f}, "
        f"MRR={hybrid['mrr']:.3f}, pokrivenost kataloga={hybrid['catalog_coverage_at_10']:.1%} "
        f"i prosječna raznolikost liste={hybrid['intra_list_diversity']:.3f}.",
        "",
        "Ovi rezultati su eksplorativna studija slučaja jednog korisnika i ne predstavljaju "
        "procjenu kvalitete za populaciju korisnika.",
        "",
        "## Cold-start novih pjesama",
        "",
        f"U katalogu je pronađeno {cold_item_count} analiziranih pjesama bez interakcija i s nula streamova. "
        f"Na {len(cold_start)} kontroliranih seed-slučajeva radio-preporuke vratile su prosječno "
        f"{cold_means['SameClusterAt10']:.1%} pjesama iz istog klastera, prosječni Jaccard "
        f"audio-oznaka {cold_means['TagJaccardAt10']:.3f} i kosinusnu sličnost "
        f"{cold_means['EmbeddingSimilarityAt10']:.3f}.",
        "",
        "## Operativna pouzdanost",
        "",
        f"Audio-analiza pokriva {operational['analysis_coverage']:.1%} kataloga. Stopa uspješnosti "
        f"završenih poslova audio-analize je {operational['analysis_success_rate_terminal']:.1%}. "
        f"Stopa uspješnosti završenih Demucs poslova je {operational['stem_success_rate_terminal']:.1%}; "
        f"medijan trajanja je {operational['stem_latency_median_seconds']:.1f} s, a P95 "
        f"{operational['stem_latency_p95_seconds']:.1f} s.",
        "",
        f"Od {operational['stem_ready']} spremnih stem setova, "
        f"{operational['ready_stem_sets_with_exactly_four_stems']} sadrži točno četiri stem datoteke.",
        "",
        "## Ograničenja",
        "",
        "- Nema referentnih izoliranih stemova, pa se SDR/SIR/SAR ne može valjano izračunati.",
        "- Povijesni poslovi ne bilježe korišteni uređaj, pa CPU/GPU usporedba zahtijeva zaseban kontrolirani benchmark.",
        "- IP5 (korisničko ispitivanje stem reproduktora) namjerno nije proveden ovom skriptom.",
    ]
    (RESULTS / "summary.md").write_text("\n".join(lines), encoding="utf-8")


def main():
    (
        catalog,
        analyzed,
        x,
        track_ids,
        id_to_index,
        tags_df,
        tags_by_track,
        _active_run_id,
        cluster_by_track,
        artists_by_track,
        interactions,
    ) = load_inputs()

    clustering, selected_key, selected_labels = evaluate_clustering(x, track_ids, tags_by_track)
    episodes, recommendation_metrics, positive_count = evaluate_recommendations(
        analyzed,
        x,
        id_to_index,
        tags_by_track,
        cluster_by_track,
        artists_by_track,
        interactions,
    )
    cold_start, cold_item_count = evaluate_cold_start(
        analyzed,
        x,
        id_to_index,
        tags_by_track,
        cluster_by_track,
        artists_by_track,
        interactions,
    )
    operational, stem_sets = evaluate_operational(catalog)
    make_figures(
        clustering,
        selected_key,
        selected_labels,
        x,
        recommendation_metrics,
        cold_start,
        stem_sets,
        operational,
    )
    write_summary(
        catalog,
        x,
        tags_df,
        clustering,
        selected_key,
        episodes,
        recommendation_metrics,
        positive_count,
        cold_start,
        cold_item_count,
        operational,
    )
    print((RESULTS / "summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
