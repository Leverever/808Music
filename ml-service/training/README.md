# Modern genre head training

This directory contains the reproducible starting point for training a
multi-label classifier head on the same 1,280-dimensional Discogs-EffNet
embeddings used by the production worker.

## Dataset contract

Copy `dataset.example.csv` to a working location and replace the example rows.
Each row represents one full track:

```csv
track_id,artist_id,audio_path,labels,split
123,artist-42,/data/audio/123.wav,phonk|drift_phonk,train
```

- `track_id` must be unique and filesystem-safe.
- `artist_id` is required so the trainer can reject artist leakage between
  train, validation, and test splits.
- `labels` is a `|`-separated, exhaustive list of labels from
  `modern_genres_v1.json`. An empty value means the track is a negative example
  for every label.
- `split` must be `train`, `validation`, or `test`.
- Child labels automatically imply their configured parent labels.

Only use audio and annotations that 808Music has permission to use for model
training. Do not treat predictions from the existing classifiers as ground
truth for the missing genres.

Embedding extraction writes `embedding-manifest.json` with hashes of the
dataset manifest and exact Discogs-EffNet graph. Training refuses mismatched
embeddings, which prevents silently training against a different backbone or
dataset revision. If either input changes, rerun embedding extraction with
`--force`.

## Run with Docker

From `ml-service`:

```powershell
docker build -f training/Dockerfile -t 808music-head-training .
```

Mount the dataset, audio, pretrained model, and a writable work directory:

```powershell
docker run --rm `
  -v C:\training-data:/data `
  -v C:\training-work:/work `
  808music-head-training `
  python training/prepare_embeddings.py `
    --dataset /data/dataset.csv `
    --model /data/discogs-effnet-bs64-1.pb `
    --output-dir /work/embeddings
```

Train, calibrate thresholds, evaluate on the test split, and export:

```powershell
docker run --rm `
  -v C:\training-data:/data `
  -v C:\training-work:/work `
  808music-head-training `
  python training/train_head.py `
    --dataset /data/dataset.csv `
    --taxonomy training/modern_genres_v1.json `
    --embeddings-dir /work/embeddings `
    --output-dir /work/artifacts
```

Run a linear transfer-learning baseline first by adding `--hidden-units 0`.
Then compare it with the default 512-unit hidden layer using the same splits.
Only retain the more complex head if it materially improves the held-out
per-label metrics.

The export contains:

```text
808music-modern-genres-discogs-effnet-1.pb
808music-modern-genres-discogs-effnet-1.json
808music-modern-genres-discogs-effnet-1.weights.h5
808music-modern-genres-discogs-effnet-1.metrics.json
custom-heads.json
```

Copy the `.pb`, model `.json`, and `custom-heads.json` into
`ESSENTIA_MODEL_DIR`. On the next worker start, the custom head is loaded
automatically. Keep the weights and metrics files as training artifacts; the
production worker does not require them.

## Before enabling recommendations

Review per-label precision and recall in the metrics artifact, listen to false
positives, and adjust the taxonomy or dataset where needed. Run the model in
shadow mode with its manifest entry set to `"enabled": false` until its
thresholds are accepted. After enabling it, bump the backend audio-analysis
model version and re-run analysis for existing tracks.
