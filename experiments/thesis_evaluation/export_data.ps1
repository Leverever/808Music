param(
    [string]$ServerInstance = "localhost",
    [string]$Database = "naziv_db_2",
    [string]$OutputDirectory = "$PSScriptRoot\data"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

Import-Module SQLPS -DisableNameChecking

function Export-Query {
    param(
        [Parameter(Mandatory = $true)][string]$FileName,
        [Parameter(Mandatory = $true)][string]$Query
    )

    $targetPath = Join-Path $OutputDirectory $FileName
    Invoke-Sqlcmd `
        -ServerInstance $ServerInstance `
        -Database $Database `
        -Query $Query `
        -MaxCharLength 2097152 `
        -QueryTimeout 180 |
        Export-Csv -LiteralPath $targetPath -NoTypeInformation -Encoding utf8

    Write-Host "Exported $FileName"
}

Export-Query -FileName "catalog.csv" -Query @"
SET NOCOUNT ON;
SELECT
    t.Id AS TrackId,
    t.Title,
    t.Length AS DurationSeconds,
    t.Streams,
    t.TrackPath,
    t.AlbumId,
    a.ArtistId AS AlbumArtistId,
    a.ReleaseDate,
    taa.Id AS AnalysisId,
    taa.ProviderName,
    taa.ModelName,
    taa.ModelVersion,
    taa.EmbeddingModel,
    taa.EmbeddingJson
FROM Tracks AS t
LEFT JOIN Albums AS a ON a.Id = t.AlbumId
OUTER APPLY (
    SELECT TOP (1)
        x.Id,
        x.ProviderName,
        x.ModelName,
        x.ModelVersion,
        x.EmbeddingModel,
        x.EmbeddingJson
    FROM TrackAudioAnalyses AS x
    WHERE x.TrackId = t.Id
      AND x.Status = 3
      AND x.IsActive = 1
    ORDER BY COALESCE(x.CompletedAt, x.CreatedAt) DESC
) AS taa
ORDER BY t.Id;
"@

Export-Query -FileName "artist_tracks.csv" -Query @"
SET NOCOUNT ON;
SELECT TrackId, ArtistId, IsLead
FROM ArtistsTracks
ORDER BY TrackId, IsLead DESC, ArtistId;
"@

Export-Query -FileName "audio_analyses.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS AnalysisId,
    TrackId,
    Status,
    ProviderName,
    ModelName,
    ModelVersion,
    EmbeddingModel,
    IsActive,
    CreatedAt,
    StartedAt,
    CompletedAt,
    ErrorMessage
FROM TrackAudioAnalyses
ORDER BY CreatedAt;
"@

Export-Query -FileName "audio_tags.csv" -Query @"
SET NOCOUNT ON;
SELECT
    tag.Id AS TagId,
    tag.TrackAudioAnalysisId AS AnalysisId,
    analysis.TrackId,
    tag.Namespace,
    tag.Label,
    tag.Score,
    tag.ModelName
FROM TrackAudioTags AS tag
INNER JOIN TrackAudioAnalyses AS analysis
    ON analysis.Id = tag.TrackAudioAnalysisId
WHERE analysis.Status = 3
  AND analysis.IsActive = 1
ORDER BY analysis.TrackId, tag.Namespace, tag.Score DESC;
"@

Export-Query -FileName "cluster_runs.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS ClusterRunId,
    Status,
    AlgorithmName,
    EmbeddingSource,
    ParametersJson,
    IsActive,
    CreatedAt,
    StartedAt,
    CompletedAt,
    ErrorMessage
FROM AudioClusterRuns
ORDER BY CreatedAt;
"@

Export-Query -FileName "clusters.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS ClusterId,
    ClusterRunId,
    ClusterKey,
    Name,
    Size,
    TopTagsJson,
    CreatedAt
FROM AudioClusters
ORDER BY ClusterRunId, ClusterKey;
"@

Export-Query -FileName "cluster_assignments.csv" -Query @"
SET NOCOUNT ON;
SELECT
    assignment.Id AS AssignmentId,
    assignment.ClusterRunId,
    run.AlgorithmName,
    run.IsActive AS IsActiveRun,
    assignment.ClusterId,
    assignment.TrackId,
    assignment.ClusterKey,
    assignment.IsNoise,
    assignment.DistanceToCenter,
    assignment.MembershipScore,
    assignment.CreatedAt
FROM TrackClusterAssignments AS assignment
INNER JOIN AudioClusterRuns AS run
    ON run.Id = assignment.ClusterRunId
ORDER BY assignment.ClusterRunId, assignment.TrackId;
"@

Export-Query -FileName "interactions.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS InteractionId,
    UserId,
    TrackId,
    InteractionType,
    OccurredAt,
    PlayedMs,
    TrackDurationMs,
    CompletionRatio,
    ContextType,
    ClientEventId,
    CreatedAt
FROM UserTrackInteractions
ORDER BY UserId, OccurredAt, CreatedAt;
"@

Export-Query -FileName "profile_caches.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS ProfileCacheId,
    UserId,
    ProfileDate,
    GeneratedAt,
    SourceInteractionCount,
    SourceWindowDays,
    EmbeddingJson,
    TagAffinitiesJson,
    ClusterAffinitiesJson,
    RecentTrackIdsJson,
    FavoriteArtistIdsJson,
    FavoriteAlbumIdsJson
FROM UserMusicProfileCaches
ORDER BY UserId, ProfileDate;
"@

Export-Query -FileName "generated_playlists.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS PlaylistId,
    UserId,
    ThemeKey,
    Name,
    Description,
    PlaylistDate,
    CreatedAt,
    ThemeId
FROM GeneratedPersonalizedPlaylists
ORDER BY CreatedAt;
"@

Export-Query -FileName "generated_playlist_tracks.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS PlaylistTrackId,
    PlaylistId,
    TrackId,
    Position,
    Score,
    Reason
FROM GeneratedPersonalizedPlaylistTracks
ORDER BY PlaylistId, Position;
"@

Export-Query -FileName "stem_sets.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS StemSetId,
    TrackId,
    Source,
    Status,
    ModelName,
    ModelVersion,
    ProviderName,
    StemProfile,
    IsActive,
    CreatedAt,
    StartedAt,
    CompletedAt,
    ErrorMessage
FROM TrackStemSets
ORDER BY CreatedAt;
"@

Export-Query -FileName "stems.csv" -Query @"
SET NOCOUNT ON;
SELECT
    Id AS StemId,
    StemSetId,
    StemType,
    ObjectKey,
    ContentType,
    SizeBytes,
    DurationMs,
    SampleRate,
    BitrateKbps,
    Codec,
    Channels,
    ChecksumSha256,
    CreatedAt
FROM TrackStems
ORDER BY StemSetId, StemType;
"@

Write-Host "Evaluation data exported to $OutputDirectory"
