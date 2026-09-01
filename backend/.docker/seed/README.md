# Local database seed

`scripts/docker-up.ps1` writes `local-database.bak` here when it finds an
existing local `naziv_db_2` SQL Server database, or when `-DatabaseBackup` is
provided. Backup files in this directory are intentionally ignored by Git and
are preferred over the repository backup during the SQL Server image build.
