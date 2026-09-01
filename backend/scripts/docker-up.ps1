[CmdletBinding()]
param(
    [string]$LocalSqlServer = "localhost",
    [string]$DatabaseName = "naziv_db_2",
    [string]$DatabaseBackup,
    [switch]$SkipLocalDatabaseDetection,
    [switch]$PrepareOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

if ($DatabaseName -notmatch '^[A-Za-z0-9_-]+$') {
    throw "DatabaseName may contain only letters, digits, underscores, and hyphens."
}

$backendRoot = Split-Path -Parent $PSScriptRoot
$seedDirectory = Join-Path $backendRoot ".docker\seed"
$seedBackup = Join-Path $seedDirectory "local-database.bak"
$partialBackup = Join-Path $seedDirectory "local-database.partial"
$composeFile = Join-Path $backendRoot "docker-compose.yml"

New-Item -ItemType Directory -Path $seedDirectory -Force | Out-Null

function Copy-DatabaseBackup {
    param([Parameter(Mandatory = $true)][string]$Source)

    $resolvedSource = (Resolve-Path -LiteralPath $Source).Path
    if ([System.IO.Path]::GetExtension($resolvedSource) -ne ".bak") {
        throw "The database backup must be a .bak file: $resolvedSource"
    }

    if ($resolvedSource -ne $seedBackup) {
        Copy-Item -LiteralPath $resolvedSource -Destination $seedBackup -Force
    }

    Write-Host "Prepared database seed from $resolvedSource"
}

if ($DatabaseBackup) {
    Copy-DatabaseBackup -Source $DatabaseBackup
}
elseif (-not $SkipLocalDatabaseDetection) {
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue

    if ($null -eq $sqlcmd) {
        Write-Warning "sqlcmd is not installed; skipping live local SQL Server detection."
    }
    else {
        $escapedDatabaseName = $DatabaseName.Replace("'", "''")
        $databaseExistsQuery = "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'$escapedDatabaseName') IS NULL THEN 0 ELSE 1 END;"
        try {
            $databaseExists = & $sqlcmd.Source `
                -S $LocalSqlServer `
                -E `
                -b `
                -l 5 `
                -h -1 `
                -W `
                -Q $databaseExistsQuery 2>$null
            $databaseCheckExitCode = $LASTEXITCODE
        }
        catch {
            $databaseExists = $null
            $databaseCheckExitCode = 1
        }

        if ($databaseCheckExitCode -eq 0 -and ($databaseExists | Out-String).Trim() -eq "1") {
            $sqlBackupPath = $partialBackup.Replace("'", "''")
            $backupQuery = "BACKUP DATABASE [$DatabaseName] TO DISK = N'$sqlBackupPath' WITH COPY_ONLY, INIT, COMPRESSION, CHECKSUM;"

            Write-Host "Found $DatabaseName on $LocalSqlServer; creating a Docker seed backup..."
            & $sqlcmd.Source `
                -S $LocalSqlServer `
                -E `
                -b `
                -Q $backupQuery

            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $partialBackup)) {
                throw "SQL Server could not create the local seed backup. Use -DatabaseBackup with an existing .bak file."
            }

            Move-Item -LiteralPath $partialBackup -Destination $seedBackup -Force
            Write-Host "Prepared database seed from the live local database."
        }
        elseif (Test-Path -LiteralPath $seedBackup) {
            Write-Warning "The live local database was not found or could not be queried; retaining the previously prepared seed."
        }
        else {
            Write-Host "No accessible live local database was found. The repository backup will be used."
        }
    }
}

$wwwroot = Join-Path $backendRoot "RS1_2024_25.API\wwwroot"
$wwwrootFiles = @(Get-ChildItem -LiteralPath $wwwroot -Recurse -File -ErrorAction SilentlyContinue)
$wwwrootSize = ($wwwrootFiles | Measure-Object -Property Length -Sum).Sum
if ($null -eq $wwwrootSize) { $wwwrootSize = 0 }
Write-Host ("Backend image input: {0} wwwroot files ({1:N1} MiB)." -f $wwwrootFiles.Count, ($wwwrootSize / 1MB))

$trackFiles = Join-Path $backendRoot "RS1_2024_25.API\TrackFiles"
$legacyTracks = @(Get-ChildItem -LiteralPath $trackFiles -Recurse -File -ErrorAction SilentlyContinue)
$trackSize = ($legacyTracks | Measure-Object -Property Length -Sum).Sum
if ($null -eq $trackSize) { $trackSize = 0 }
Write-Host ("Backend image input: {0} legacy track files ({1:N1} MiB)." -f $legacyTracks.Count, ($trackSize / 1MB))

if ($PrepareOnly) {
    Write-Host "Docker inputs are prepared."
    exit 0
}

$env:DATABASE_NAME = $DatabaseName
$composeArguments = @(
    "compose",
    "--project-directory", $backendRoot,
    "-f", $composeFile,
    "up"
)
if (-not $NoBuild) {
    $composeArguments += "--build"
}
$composeArguments += @("--detach", "backend")

& docker @composeArguments
if ($LASTEXITCODE -ne 0) {
    throw "docker compose failed with exit code $LASTEXITCODE."
}

Write-Host "808Music backend is available at http://localhost:7000"
