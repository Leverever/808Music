from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pandas as pd


ROOT = Path(__file__).resolve().parent
DATA = ROOT / "data"
RESULTS = ROOT / "results"


def read_json(name: str):
    return json.loads((RESULTS / name).read_text(encoding="utf-8-sig"))


def clean(value):
    if isinstance(value, dict):
        return {key: clean(item) for key, item in value.items()}
    if isinstance(value, list):
        return [clean(item) for item in value]
    if isinstance(value, (np.integer,)):
        return int(value)
    if isinstance(value, (np.floating,)):
        return None if not np.isfinite(value) else float(value)
    return value


def row_dict(row):
    return clean(row.to_dict())


def main():
    catalog = pd.read_csv(DATA / "catalog.csv", dtype=str)
    interactions = pd.read_csv(DATA / "interactions.csv", dtype=str)
    interactions["OccurredAtDt"] = pd.to_datetime(interactions["OccurredAt"], dayfirst=True, format="mixed")
    clustering = pd.read_csv(RESULTS / "clustering_metrics.csv")
    recommendations = pd.read_csv(RESULTS / "recommendation_metrics.csv").set_index("variant")
    episodes = pd.read_csv(RESULTS / "recommendation_episodes.csv")
    cold_start = pd.read_csv(RESULTS / "cold_start_item_cases.csv")
    operational = read_json("operational_summary.json")
    storage = read_json("object_storage_validation.json")
    api = read_json("api_smoke_summary.json")
    demucs = read_json("demucs_benchmark.json")
    sync = read_json("stem_sync_browser.json")

    representative_clusters = []
    for algorithm in ("kmeans", "agglomerative", "hdbscan"):
        representative_clusters.append(
            row_dict(
                clustering[clustering["algorithm"] == algorithm]
                .sort_values("selection_score", ascending=False)
                .iloc[0]
            )
        )

    recommendation_names = {
        "POP": "pop",
        "FRESH-POP": "fresh_pop",
        "CONTENT-SEED": "content_seed",
        "CONTENT-PROFILE": "embedding_only",
        "TAG-CLUSTER": "tag_cluster_only",
        "HYBRID-NODIV": "no_diversity",
        "HYBRID": "hybrid",
    }
    recommendation_table = {
        label: row_dict(recommendations.loc[variant])
        for label, variant in recommendation_names.items()
    }
    hybrid = recommendations.loc["hybrid"]
    ablation_names = {
        "bez audio-ugradnje": "no_embedding",
        "bez oznaka": "no_tags",
        "bez klastera": "no_clusters",
        "bez vremenskog slabljenja": "no_decay",
        "bez novosti": "no_novelty",
        "bez ograničenja raznolikosti": "no_diversity",
    }
    ablations = {}
    for label, variant in ablation_names.items():
        row = recommendations.loc[variant]
        ablations[label] = {
            "delta_ndcg_at_10": float(row["ndcg_at_10"] - hybrid["ndcg_at_10"]),
            "delta_coverage_at_10": float(row["catalog_coverage_at_10"] - hybrid["catalog_coverage_at_10"]),
            "delta_diversity": float(row["intra_list_diversity"] - hybrid["intra_list_diversity"]),
        }

    benchmark_frame = pd.DataFrame(demucs["results"])
    demucs_groups = []
    for (device, profile), group in benchmark_frame.groupby(["device", "profile"], sort=False):
        demucs_groups.append(
            {
                "device": device,
                "profile": profile,
                "jobs": int(len(group)),
                "median_seconds": float(group["wall_time_seconds"].median()),
                "p95_seconds": float(group["wall_time_seconds"].quantile(0.95)),
                "success_rate": float(group["success"].mean()),
                "median_output_mb": float(group["output_size_bytes"].median() / (1024 * 1024)),
                "median_real_time_factor": float(group["real_time_factor"].median()),
            }
        )

    unique_episodes = episodes[episodes["Variant"] == "hybrid"].copy()
    evaluated_cutoffs = pd.to_datetime(unique_episodes["Cutoff"])
    cold_means = cold_start[["SameClusterAt10", "TagJaccardAt10", "EmbeddingSimilarityAt10"]].mean()

    payload = {
        "tests": {
            "backend": {"passed": 36, "failed": 0, "skipped": 0, "configuration": "Release"},
            "ml_service": {"passed": 11, "failed": 0},
            "frontend": {"passed": 13, "failed": 0, "production_build": True},
        },
        "dataset": {
            "catalog_tracks": int(len(catalog)),
            "analyzed_tracks": int(catalog["AnalysisId"].notna().sum()),
            "analysis_coverage": float(catalog["AnalysisId"].notna().mean()),
            "interaction_users": int(interactions["UserId"].nunique()),
            "registered_users": 15,
            "cold_start_users_without_interactions": 14,
            "interactions": int(len(interactions)),
            "interaction_period_start": interactions["OccurredAtDt"].min().isoformat(),
            "interaction_period_end": interactions["OccurredAtDt"].max().isoformat(),
            "evaluation_episodes": int(len(unique_episodes)),
            "evaluation_start": evaluated_cutoffs.min().isoformat(),
            "evaluation_end": evaluated_cutoffs.max().isoformat(),
            "positive_events_before_eligibility_filter": 12,
        },
        "clustering": {
            "representatives": representative_clusters,
            "selected": row_dict(clustering.iloc[0]),
        },
        "recommendations": {
            "table": recommendation_table,
            "ablations": ablations,
            "limitations": "Eksplorativna rolling-origin studija slučaja jednog korisnika s deset ciljnih epizoda.",
        },
        "cold_start": {
            "analyzed_items_without_interactions_and_streams": 67,
            "seed_cases": int(len(cold_start)),
            "same_cluster_at_10": float(cold_means["SameClusterAt10"]),
            "tag_jaccard_at_10": float(cold_means["TagJaccardAt10"]),
            "embedding_similarity_at_10": float(cold_means["EmbeddingSimilarityAt10"]),
        },
        "operational": operational,
        "storage": storage,
        "api": api,
        "demucs_benchmark": {
            "model": demucs["model"],
            "gpu": demucs["gpu"],
            "torch_version": demucs["torch_version"],
            "groups": demucs_groups,
        },
        "stem_sync": sync,
        "research_answers": {
            "IP1": (
                "Nije potvrđeno da hibrid poboljšava relevantnost: na deset epizoda ostvario je "
                f"NDCG@10={hybrid['ndcg_at_10']:.3f} i Recall@10={hybrid['recall_at_10']:.3f}, "
                f"dok je CONTENT-PROFILE ostvario NDCG@10={recommendations.loc['embedding_only', 'ndcg_at_10']:.3f}. "
                "Hibrid je ipak obuhvatio veći dio kataloga od CONTENT-PROFILE pristupa."
            ),
            "IP2": (
                "Sadržajni signali omogućili su preporuke za 67 analiziranih pjesama bez interakcija i streamova; "
                f"u 12 seed-slučajeva prosjek istog klastera u prvih deset iznosio je {cold_means['SameClusterAt10']:.1%}."
            ),
            "IP3": (
                f"Odabrani HDBSCAN (min_cluster_size=5) dao je {int(clustering.iloc[0]['cluster_count'])} klastera, "
                f"{clustering.iloc[0]['noise_ratio']:.1%} šuma, siluetu {clustering.iloc[0]['silhouette_cosine']:.3f} "
                f"i stabilnost ARI {clustering.iloc[0]['stability_ari']:.3f}."
            ),
            "IP4": (
                f"Od {operational['stem_jobs']} povijesnih poslova {operational['stem_ready']} je završilo uspješno, "
                f"svih {operational['stem_ready']} spremnih setova ima četiri datoteke, a svih {storage['unique_stem_keys']} "
                "stem objekata je dohvatljivo. U najtežem prirodnom browser-scenariju P95 odmaka ostao je ispod 80 ms."
            ),
            "IP5": "Korisnička studija stem-playera nije provedena; ovaj odgovor ostaje za autora rada.",
        },
    }

    cleaned = clean(payload)
    (RESULTS / "final_results.json").write_text(
        json.dumps(cleaned, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    lines = [
        "# Konačni tehnički rezultati",
        "",
        *[f"- **{key}:** {value}" for key, value in cleaned["research_answers"].items()],
        "",
        "IP5 je jedina namjerno nedovršena istraživačka točka.",
    ]
    (RESULTS / "final_summary.md").write_text("\n".join(lines), encoding="utf-8")
    print((RESULTS / "final_summary.md").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
