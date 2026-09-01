# Dockerized backend

The Compose stack runs the ASP.NET Core API, SQL Server, Redis, RabbitMQ, and
MinIO. SQL data, uploaded/static files, legacy tracks, queue data, and object
storage all use named Docker volumes.

## First start on Windows

From the repository root, run:

```powershell
.\backend\scripts\docker-up.ps1
```

Before building, the script:

1. Uses `sqlcmd` with Windows authentication to check `localhost` for the
   `naziv_db_2` database.
2. Creates a copy-only backup in `backend/.docker/seed` when that database is
   available.
3. Otherwise keeps a previously prepared backup, or falls back to the checked-in
   backup under `db-backups`.
4. Reports the local `wwwroot` and `TrackFiles` files that Docker will copy into
   the backend image.

To use a particular backup or SQL Server instance:

```powershell
.\backend\scripts\docker-up.ps1 -DatabaseBackup C:\backups\808music.bak
.\backend\scripts\docker-up.ps1 -LocalSqlServer '.\SQLEXPRESS'
```

If SQL Server cannot write into the repository directory, create a `.bak`
manually and pass it with `-DatabaseBackup`. Use `-PrepareOnly` to stage and
inspect inputs without starting Docker.

The API and Swagger UI are exposed at `http://localhost:7000`. SQL Server is
exposed at `localhost,1433`; Redis, RabbitMQ, and MinIO retain their existing
ports.

## Initialization behavior

- The SQL initializer restores the selected backup only when `naziv_db_2` does
  not already exist in the `sqlserver-data` volume. It never overwrites an
  existing Docker database.
- Pending clean-architecture EF Core migrations run after restore and before the
  API starts accepting requests.
- The `backend-wwwroot` and `backend-track-files` volumes are populated by
  Docker from the files embedded in the backend image when those volumes are
  first created. Later container/image rebuilds preserve the volume contents.

You can also start the core stack directly from `backend`:

```powershell
docker compose up --build --detach backend
```

Direct Compose startup cannot inspect a live host SQL Server process, so it uses
an already staged `.docker/seed/local-database.bak` or the repository backup.

To inspect status or logs:

```powershell
docker compose ps
docker compose logs -f backend database-init sqlserver
```

Copy `.env.example` to `.env` to change ports or the SQL Server password. When
using the helper script with a different database name, also pass
`-DatabaseName your_database`. Do not commit real credentials.

## Testing MinIO through a tunnel

The backend uses `S3__ServiceUrl=http://minio:9000` for private container-to-
container access and `MINIO_PUBLIC_URL` when it creates presigned object URLs
for the frontend. To make those object URLs reachable from another device,
start a third tunnel for the MinIO S3 API (port `9000`, not the console on
`9001`):

```powershell
cloudflared tunnel --config NUL --url http://localhost:9000
```

Copy `.env.example` to `.env`, set the generated URL without a trailing slash,
and recreate the backend container so it reads the new value:

```dotenv
MINIO_PUBLIC_URL=https://your-minio-tunnel.trycloudflare.com
```

```powershell
cd backend
docker compose up --detach --force-recreate backend
```

Quick Tunnel hostnames change whenever `cloudflared` restarts. Update
`MINIO_PUBLIC_URL` and recreate the backend whenever the MinIO tunnel URL
changes. Existing presigned URLs also expire normally; request fresh API data
after changing the hostname.
