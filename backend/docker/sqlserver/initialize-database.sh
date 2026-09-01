#!/usr/bin/env bash
set -euo pipefail

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD must be set}"

database_name="${DATABASE_NAME:-naziv_db_2}"
sql_host="${SQLSERVER_HOST:-sqlserver}"
backup_path="/opt/808music/seed/database.bak"

if [[ ! "$database_name" =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo "DATABASE_NAME may contain only letters, digits, underscores, and hyphens." >&2
  exit 2
fi

echo "Waiting for SQL Server at ${sql_host}..."
until /opt/808music/scripts/sqlcmd.sh \
  -S "$sql_host" \
  -U sa \
  -P "$MSSQL_SA_PASSWORD" \
  -C \
  -b \
  -Q "SELECT 1" \
  -o /dev/null; do
  sleep 2
done

if [ -f "$backup_path" ]; then
  /opt/808music/scripts/sqlcmd.sh \
    -S "$sql_host" \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -C \
    -b \
    -v DatabaseName="$database_name" BackupPath="$backup_path" \
    -i /opt/808music/scripts/restore-database.sql
else
  echo "No seed backup is available. Creating an empty ${database_name} database."
  /opt/808music/scripts/sqlcmd.sh \
    -S "$sql_host" \
    -U sa \
    -P "$MSSQL_SA_PASSWORD" \
    -C \
    -b \
    -Q "IF DB_ID(N'${database_name}') IS NULL CREATE DATABASE [${database_name}];"
fi
