# 808Music ML Worker

Pipeline worker for long-running ML jobs.

Current job:

- consume stem separation requests from RabbitMQ
- download the source track from S3-compatible storage
- run the configured stem separator provider
- upload separated stems to object storage
- report completion or failure back to the .NET backend

The worker can also run the Essentia audio-analysis pipeline in a separate
process. That pipeline consumes `ml.audio.analysis` jobs, downloads the master
track, extracts Discogs-EffNet embeddings, emits the strongest labels from the
built-in Discogs-400 style classifier, runs MTG-Jamendo multi-label heads, and
reports the result to `/api/internal/audio-analysis/{analysisId}/complete`. It
can also run `audio-clustering` jobs with interchangeable clustering algorithms
such as K-Means, HDBSCAN, and agglomerative clustering.

The worker uses ports and adapters:

- inbound adapter: RabbitMQ consumer
- outbound adapters: S3 storage, .NET callback client, Demucs separator
- application core: provider registry and stem separation pipeline

Run locally through the backend compose file. The default worker uses GPU acceleration:

```powershell
docker compose -f backend/docker-compose.yml up --build
```

For CPU-only mode, explicitly start the CPU worker profile and service:

```powershell
docker compose -f backend/docker-compose.yml --profile cpu up --build rabbitmq minio minio-init ml-worker
```

For Essentia audio analysis, the worker auto-downloads missing model files to
`/models/essentia` by default. To run without internet access, place these files
under the `ml-worker-models` volume at `/models/essentia` and set
`ESSENTIA_AUTO_DOWNLOAD_MODELS=false`:

```txt
discogs-effnet-bs64-1.pb
discogs-effnet-bs64-1.json
mtg_jamendo_top50tags-discogs-effnet-1.pb
mtg_jamendo_top50tags-discogs-effnet-1.json
mtg_jamendo_genre-discogs-effnet-1.pb
mtg_jamendo_genre-discogs-effnet-1.json
mtg_jamendo_moodtheme-discogs-effnet-1.pb
mtg_jamendo_moodtheme-discogs-effnet-1.json
```

Discogs-400 tagging is enabled by default. The model evaluates all 400 classes
but only emits the strongest configured results. Discogs parent genres are
stored as namespaces such as `discogs.electronic`, while canonical leaf labels
such as `Hardstyle` are stored as labels.

```txt
ESSENTIA_DISCOGS_TAGS_ENABLED=true
ESSENTIA_DISCOGS_TOP_K=8
ESSENTIA_DISCOGS_MIN_SCORE=0.15
```

`Non-Music` Discogs classes are not emitted. The current implementation uses
separate Essentia predictor instances for embeddings and Discogs probabilities.
This favors the supported high-level Essentia APIs but runs the base graph
twice. It can later be optimized to a shared low-level TensorFlow session if
analysis throughput requires it.

Additional classifier heads can be installed without changing worker code.
Place their `.pb` and `.json` artifacts in `ESSENTIA_MODEL_DIR` and add
`custom-heads.json`:

```json
{
  "heads": [
    {
      "namespace": "modern_genre",
      "modelName": "808music-modern-genres-discogs-effnet-1",
      "enabled": true,
      "topK": 8,
      "minScore": 0.1
    }
  ]
}
```

The model metadata may provide a `tagging.thresholds` object for calibrated
per-label thresholds. Schema input and prediction node names are also read from
the metadata, allowing newly exported frozen graphs to use their actual node
names.

The reproducible modern-head dataset, embedding, training, calibration and
export workflow is documented in [training/README.md](training/README.md).

Then start the analysis worker:

```powershell
docker compose -f backend/docker-compose.yml --profile analysis up --build rabbitmq minio minio-init ml-audio-worker
```

For clustering, start the clustering worker:

```powershell
docker compose -f backend/docker-compose.yml --profile clustering up --build rabbitmq ml-clustering-worker
```

The clustering worker expects RabbitMQ messages shaped like:

```json
{
  "clusterRunId": "run-guid",
  "algorithmName": "kmeans",
  "embeddingSource": "essentia",
  "parameters": {
    "nClusters": 12,
    "randomState": 42
  }
}
```

Supported `algorithmName` values are `kmeans`, `hdbscan`, and `agglomerative`.

GPU mode requires an NVIDIA GPU, a compatible host driver, and Docker GPU support.
The worker uses the CUDA Dockerfile, installs CUDA-enabled PyTorch, and runs Demucs
with `DEMUCS_DEVICE=cuda`.

The backend must be reachable at `BACKEND_BASE_URL` and must use the same `BACKEND_INTERNAL_API_KEY`.

Manual artist-uploaded stems do not go through this worker. They are uploaded directly to the backend:

```http
POST /api/v2/tracks/{trackId}/stems/upload
Content-Type: multipart/form-data
Authorization: Bearer <token>
```

For `four-stem`, send:

```txt
stemProfile=four-stem
vocals=<file>
drums=<file>
bass=<file>
other=<file>
```

For `two-stem-vocals`, send:

```txt
stemProfile=two-stem-vocals
vocals=<file>
instrumental=<file>
```
