from __future__ import annotations

import json
import math
from collections import defaultdict
from pathlib import Path

import pandas as pd
from PIL import Image, ImageDraw

from evaluate import DATA, FIGURES, RESULTS, bar_chart, font, normalize_tag, number, read_csv


def semantic_profile():
    assignments = pd.read_csv(RESULTS / "selected_cluster_assignments.csv")
    tags = read_csv("audio_tags.csv")
    tags["TrackId"] = number(tags["TrackId"]).astype(int)
    tags["ScoreNum"] = number(tags["Score"]).fillna(0.0)
    tags["Normalized"] = tags["Label"].map(normalize_tag)
    merged = tags.merge(assignments, on="TrackId")
    merged = merged[merged["Cluster"] != -1]
    aggregate = merged.groupby(["Cluster", "Normalized"], as_index=False)["ScoreNum"].sum()

    labels = set()
    for cluster in sorted(aggregate["Cluster"].unique()):
        selected = aggregate[aggregate["Cluster"] == cluster].nlargest(5, "ScoreNum")
        labels.update(selected["Normalized"].tolist())
    label_scores = aggregate.groupby("Normalized")["ScoreNum"].sum().sort_values(ascending=False)
    ordered_labels = [label for label in label_scores.index if label in labels][:14]
    clusters = sorted(int(value) for value in aggregate["Cluster"].unique())

    matrix = []
    for cluster in clusters:
        scores = aggregate[aggregate["Cluster"] == cluster].set_index("Normalized")["ScoreNum"]
        row = [float(scores.get(label, 0.0)) for label in ordered_labels]
        maximum = max(row + [1e-9])
        matrix.append([value / maximum for value in row])

    width, height = 1650, 700
    left, top, right, bottom = 190, 145, width - 90, height - 115
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    draw.text((width / 2, 35), "Semantički profil klastera odabranog HDBSCAN rješenja", fill="#111827", font=font(31, True), anchor="ma")
    cell_width = (right - left) / len(ordered_labels)
    cell_height = (bottom - top) / len(clusters)
    for row_index, cluster in enumerate(clusters):
        draw.text((left - 20, top + (row_index + 0.5) * cell_height), f"Klaster {cluster}", fill="#374151", font=font(20, True), anchor="rm")
        for column_index, value in enumerate(matrix[row_index]):
            x0 = left + column_index * cell_width
            y0 = top + row_index * cell_height
            x1 = x0 + cell_width
            y1 = y0 + cell_height
            red = int(239 - value * 202)
            green = int(246 - value * 147)
            blue = int(255 - value * 20)
            draw.rectangle((x0, y0, x1, y1), fill=(red, green, blue), outline="white", width=2)
            text_color = "white" if value > 0.55 else "#111827"
            draw.text(((x0 + x1) / 2, (y0 + y1) / 2), f"{value:.2f}", fill=text_color, font=font(16), anchor="mm")
    for column_index, label in enumerate(ordered_labels):
        x = left + (column_index + 0.5) * cell_width
        draw.text((x, bottom + 18), label, fill="#374151", font=font(16), anchor="ma")
    draw.text((width / 2, height - 38), "Vrijednosti su normalizirane unutar klastera; veća vrijednost znači jaču agregiranu oznaku.", fill="#4b5563", font=font(18), anchor="ma")
    image.save(FIGURES / "cluster_semantic_profile.png", dpi=(160, 160))


def history_quality():
    episodes = pd.read_csv(RESULTS / "recommendation_episodes.csv")
    data = episodes[episodes["Variant"] == "hybrid"].copy()
    data = data.sort_values("ProfilePositiveTracks")
    width, height = 1450, 850
    left, top, right, bottom = 130, 120, width - 90, height - 130
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    draw.text((width / 2, 35), "Količina pozitivne povijesti i rang testne pjesme", fill="#111827", font=font(31, True), anchor="ma")
    x_values = data["ProfilePositiveTracks"].astype(float).tolist()
    ranks = data["Rank"].astype(float).tolist()
    x_min, x_max = min(x_values), max(x_values)
    y_max = max(ranks) * 1.05
    for tick in range(6):
        rank = y_max * tick / 5
        y = top + (bottom - top) * tick / 5
        draw.line((left, y, right, y), fill="#e5e7eb", width=2)
        draw.text((left - 16, y), f"{rank:.0f}", fill="#4b5563", font=font(18), anchor="rm")
    draw.line((left, top, left, bottom), fill="#374151", width=3)
    draw.line((left, bottom, right, bottom), fill="#374151", width=3)
    for x_value, rank in zip(x_values, ranks):
        x = left + (right - left) * (x_value - x_min) / max(1, x_max - x_min)
        y = top + (bottom - top) * rank / y_max
        color = "#059669" if rank <= 10 else "#2563eb"
        draw.ellipse((x - 10, y - 10, x + 10, y + 10), fill=color, outline="white", width=2)
        draw.text((x, y - 14), f"{int(rank)}", fill="#111827", font=font(15), anchor="mb")
    for tick in range(int(x_min), int(x_max) + 1, max(1, math.ceil((x_max - x_min) / 8))):
        x = left + (right - left) * (tick - x_min) / max(1, x_max - x_min)
        draw.text((x, bottom + 20), str(tick), fill="#4b5563", font=font(18), anchor="ma")
    draw.text(((left + right) / 2, height - 45), "Broj pozitivno ponderiranih pjesama u profilu", fill="#374151", font=font(20), anchor="ma")
    draw.text((left + 12, top + 12), "Rang ciljne pjesme (niže je bolje)", fill="#374151", font=font(18), anchor="la")
    draw.text((right, top - 35), "Zeleno: cilj u prvih 10", fill="#059669", font=font(18), anchor="ra")
    image.save(FIGURES / "history_quality.png", dpi=(160, 160))


def demucs_scatter():
    benchmark = json.loads((RESULTS / "demucs_benchmark.json").read_text(encoding="utf-8"))
    rows = benchmark["results"]
    width, height = 1450, 900
    left, top, right, bottom = 125, 125, width - 85, height - 130
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    draw.text((width / 2, 35), "Vrijeme odvajanja prema trajanju ulaza", fill="#111827", font=font(31, True), anchor="ma")
    x_max = max(row["catalog_duration_seconds"] for row in rows) * 1.10
    y_max = max(row["wall_time_seconds"] for row in rows) * 1.10
    for tick in range(6):
        x_value = x_max * tick / 5
        x = left + (right - left) * tick / 5
        draw.line((x, top, x, bottom), fill="#f3f4f6", width=2)
        draw.text((x, bottom + 20), f"{x_value:.0f}", fill="#4b5563", font=font(18), anchor="ma")
        y_value = y_max * tick / 5
        y = bottom - (bottom - top) * tick / 5
        draw.line((left, y, right, y), fill="#e5e7eb", width=2)
        draw.text((left - 16, y), f"{y_value:.0f}", fill="#4b5563", font=font(18), anchor="rm")
    draw.line((left, bottom, right, bottom), fill="#374151", width=3)
    draw.line((left, top, left, bottom), fill="#374151", width=3)
    colors = {"cuda": "#059669", "cpu": "#dc2626"}
    for row in rows:
        x = left + (right - left) * row["catalog_duration_seconds"] / x_max
        y = bottom - (bottom - top) * row["wall_time_seconds"] / y_max
        color = colors[row["device"]]
        if row["profile"] == "four-stem":
            draw.ellipse((x - 10, y - 10, x + 10, y + 10), fill=color, outline="white", width=2)
        else:
            draw.rectangle((x - 9, y - 9, x + 9, y + 9), fill=color, outline="white", width=2)
    # Real-time line y=x.
    line_end = min(x_max, y_max)
    x_end = left + (right - left) * line_end / x_max
    y_end = bottom - (bottom - top) * line_end / y_max
    draw.line((left, bottom, x_end, y_end), fill="#6b7280", width=3)
    draw.text((x_end - 5, y_end - 8), "real-time faktor = 1", fill="#4b5563", font=font(17), anchor="rb")
    draw.text(((left + right) / 2, height - 45), "Trajanje ulazne pjesme (s)", fill="#374151", font=font(20), anchor="ma")
    draw.text((left + 12, top + 12), "Vrijeme odvajanja (s)", fill="#374151", font=font(18), anchor="la")
    legend = [("CUDA", "#059669", "circle"), ("CPU", "#dc2626", "circle"), ("four-stem", "#374151", "circle"), ("two-stem", "#374151", "square")]
    x_legend = left
    for label, color, shape in legend:
        if shape == "circle":
            draw.ellipse((x_legend, top - 48, x_legend + 18, top - 30), fill=color)
        else:
            draw.rectangle((x_legend, top - 48, x_legend + 18, top - 30), fill=color)
        draw.text((x_legend + 27, top - 39), label, fill="#374151", font=font(18), anchor="lm")
        x_legend += 40 + draw.textlength(label, font=font(18)) + 35
    image.save(FIGURES / "demucs_cpu_gpu.png", dpi=(160, 160))


def sync_chart():
    payload = json.loads((RESULTS / "stem_sync_browser.json").read_text(encoding="utf-8"))
    scenarios = payload["scenarios"][:4]
    bar_chart(
        FIGURES / "stem_sync.png",
        "Odmaci četiriju stem tokova u Headless Chromeu",
        [row["scenario"] for row in scenarios],
        [
            ("Medijan (ms)", [row["medianDriftMs"] for row in scenarios], "#2563eb"),
            ("P95 (ms)", [row["p95DriftMs"] for row in scenarios], "#059669"),
            ("Maksimum (ms)", [row["maximumDriftMs"] for row in scenarios], "#d97706"),
        ],
        y_max=80,
    )


def api_latency():
    payload = json.loads((RESULTS / "api_smoke_summary.json").read_text(encoding="utf-8-sig"))
    rows = payload["latency"]
    labels = [row["endpoint"] for row in rows]
    bar_chart(
        FIGURES / "api_latency.png",
        "Medijan latencije odabranih ruta API-ja v2",
        labels,
        [("Medijan (ms)", [row["medianMs"] for row in rows], "#2563eb")],
    )


def explanation_card():
    payload = json.loads((RESULTS / "explanation_samples.json").read_text(encoding="utf-8-sig"))
    sample = payload["radio"][0]
    width, height = 1500, 730
    image = Image.new("RGB", (width, height), "#f3f4f6")
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((70, 70, width - 70, height - 70), radius=28, fill="white", outline="#d1d5db", width=3)
    draw.text((120, 115), "Primjer objašnjene radio-preporuke", fill="#111827", font=font(31, True))
    draw.text((120, 180), f"Pjesma #{sample['trackId']}   •   rezultat {sample['score']:.3f}", fill="#374151", font=font(23))
    draw.text((120, 235), sample["reason"], fill="#2563eb", font=font(27, True))
    tags = ", ".join(sample.get("matchedTags", [])) or "nema podudarnih oznaka"
    draw.text((120, 295), f"Podudarne audio-oznake: {tags}", fill="#4b5563", font=font(21))
    draw.text((120, 345), f"Primarni klaster: {sample.get('clusterKey') or 'nije dostupan'}", fill="#4b5563", font=font(21))
    signals = sample["sourceSignals"]
    selected = ["seedSimilarity", "sharedTags", "clusterMatch", "userProfile", "freshnessPopularity"]
    y = 420
    for key in selected:
        value = float(signals.get(key, 0.0))
        draw.text((120, y), key, fill="#374151", font=font(19), anchor="lm")
        draw.rounded_rectangle((380, y - 12, 1260, y + 12), radius=12, fill="#e5e7eb")
        draw.rounded_rectangle((380, y - 12, 380 + 880 * value, y + 12), radius=12, fill="#059669")
        draw.text((1300, y), f"{value:.3f}", fill="#111827", font=font(18), anchor="lm")
        y += 48
    image.save(FIGURES / "explanation_card.png", dpi=(160, 160))


def main():
    semantic_profile()
    history_quality()
    demucs_scatter()
    sync_chart()
    api_latency()
    explanation_card()
    for path in sorted(FIGURES.glob("*.png")):
        print(path.name)


if __name__ == "__main__":
    main()
