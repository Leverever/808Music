#!/usr/bin/env bash
set -e

if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
  exec /opt/mssql-tools18/bin/sqlcmd "$@"
fi

if [ -x /opt/mssql-tools/bin/sqlcmd ]; then
  exec /opt/mssql-tools/bin/sqlcmd "$@"
fi

echo "sqlcmd was not found in the SQL Server image." >&2
exit 127
