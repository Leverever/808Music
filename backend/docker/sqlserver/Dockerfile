FROM mcr.microsoft.com/mssql/server:2022-latest

USER root

COPY db-backups/ /opt/808music/repository-backups/
COPY backend/.docker/seed/ /opt/808music/local-seed/
COPY backend/docker/sqlserver/sqlcmd.sh /opt/808music/scripts/sqlcmd.sh
COPY backend/docker/sqlserver/initialize-database.sh /opt/808music/scripts/initialize-database.sh
COPY backend/docker/sqlserver/restore-database.sql /opt/808music/scripts/restore-database.sql

# Prefer a backup captured from the developer's current local database. If one
# was not prepared, use the checked-in backup. The restored data itself lives
# in the sqlserver-data volume, not in this image layer.
RUN set -eux; \
    mkdir -p /opt/808music/seed; \
    local_backup="$(find /opt/808music/local-seed -type f -name '*.bak' -print -quit)"; \
    repository_backup="$(find /opt/808music/repository-backups -type f -name '*.bak' -print -quit)"; \
    if [ -n "$local_backup" ]; then \
      cp "$local_backup" /opt/808music/seed/database.bak; \
      echo "Using prepared local database backup: $local_backup"; \
    elif [ -n "$repository_backup" ]; then \
      cp "$repository_backup" /opt/808music/seed/database.bak; \
      echo "Using repository database backup: $repository_backup"; \
    else \
      echo "No database backup found; an empty database will be created."; \
    fi; \
    chmod +x /opt/808music/scripts/sqlcmd.sh /opt/808music/scripts/initialize-database.sh; \
    chown -R mssql:root /opt/808music

USER mssql
