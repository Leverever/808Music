using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Application.Tracks;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using _808Music.Infrastructure.AudioAnalysis;
using _808Music.Infrastructure.Stems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace _808Music.Infrastructure.TrackMigration;

public sealed class LegacyTrackMasterMigrationService : ILegacyTrackMasterMigrationService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a"
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;
    private readonly IAudioMetadataReader _audioMetadataReader;
    private readonly IAudioAnalysisService _audioAnalysisService;
    private readonly IStemSeparationService _stemSeparationService;
    private readonly LegacyTrackMigrationOptions _options;
    private readonly AudioAnalysisOptions _audioOptions;
    private readonly StemSeparationOptions _stemOptions;
    private readonly string _legacyTrackRoot;

    public LegacyTrackMasterMigrationService(
        IApplicationDbContext dbContext,
        IMediaStorage mediaStorage,
        IAudioMetadataReader audioMetadataReader,
        IAudioAnalysisService audioAnalysisService,
        IStemSeparationService stemSeparationService,
        IOptions<LegacyTrackMigrationOptions> options,
        IOptions<AudioAnalysisOptions> audioOptions,
        IOptions<StemSeparationOptions> stemOptions)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
        _audioMetadataReader = audioMetadataReader;
        _audioAnalysisService = audioAnalysisService;
        _stemSeparationService = stemSeparationService;
        _options = options.Value;
        _audioOptions = audioOptions.Value;
        _stemOptions = stemOptions.Value;
        _legacyTrackRoot = Path.GetFullPath(_options.LegacyTrackRoot);
    }

    public async Task<LegacyTrackMigrationDiscoveryResult> DiscoverAsync(
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var tracks = await _dbContext.Tracks
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new TrackProjection(x.Id, x.TrackPath, x.Length))
            .ToListAsync(cancellationToken);
        var existingTrackIds = await _dbContext.TrackMasterMigrations
            .AsNoTracking()
            .Select(x => x.TrackId)
            .ToHashSetAsync(cancellationToken);

        var alreadyObjectStorage = 0;
        var existingMigrationCount = 0;
        var candidates = 0;
        var created = 0;
        var missing = 0;
        var unsupported = 0;
        var now = DateTime.UtcNow;

        foreach (var track in tracks)
        {
            if (IsObjectStorageKey(track.TrackPath))
            {
                alreadyObjectStorage++;
                continue;
            }

            if (existingTrackIds.Contains(track.Id))
            {
                existingMigrationCount++;
                continue;
            }

            candidates++;
            if (string.IsNullOrWhiteSpace(track.TrackPath))
            {
                missing++;
                continue;
            }

            var migration = new TrackMasterMigration(track.Id, track.TrackPath, now);
            string? discoveryError = null;

            try
            {
                var sourcePath = ResolveLegacyPath(track.TrackPath);
                if (!File.Exists(sourcePath))
                {
                    missing++;
                    discoveryError = "Legacy master file was not found.";
                }
                else if (!SupportedExtensions.Contains(Path.GetExtension(sourcePath)))
                {
                    unsupported++;
                    discoveryError = "Legacy master file type is unsupported.";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                missing++;
                discoveryError = ex.Message;
            }

            if (dryRun)
            {
                continue;
            }

            if (discoveryError is not null)
            {
                migration.MarkFailed(discoveryError, now);
            }

            await _dbContext.TrackMasterMigrations.AddAsync(migration, cancellationToken);
            existingTrackIds.Add(track.Id);
            created++;
        }

        if (!dryRun && created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new LegacyTrackMigrationDiscoveryResult(
            dryRun,
            tracks.Count,
            alreadyObjectStorage,
            existingMigrationCount,
            candidates,
            created,
            missing,
            unsupported);
    }

    public async Task<LegacyTrackMigrationRunResult> RunBatchAsync(
        int batchSize,
        bool queueAnalysis,
        bool queueStems,
        CancellationToken cancellationToken = default)
    {
        var take = NormalizeBatchSize(batchSize);
        var migrationIds = await _dbContext.TrackMasterMigrations
            .AsNoTracking()
            .Where(x => x.Status != TrackMasterMigrationStatus.Completed &&
                x.Status != TrackMasterMigrationStatus.Failed)
            .OrderBy(x => x.UpdatedAt)
            .ThenBy(x => x.TrackId)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return await ProcessMigrationsAsync(
            migrationIds,
            queueAnalysis,
            queueStems,
            forceFailedJobRetry: false,
            cancellationToken);
    }

    public async Task<LegacyTrackMigrationRunResult> RetryAsync(
        int trackId,
        bool queueAnalysis,
        bool queueStems,
        CancellationToken cancellationToken = default)
    {
        var migration = await _dbContext.TrackMasterMigrations
            .FirstOrDefaultAsync(x => x.TrackId == trackId, cancellationToken);
        if (migration is null)
        {
            throw new KeyNotFoundException("Track master migration was not found.");
        }

        migration.PrepareRetry(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await ProcessMigrationsAsync(
            [migration.Id],
            queueAnalysis,
            queueStems,
            forceFailedJobRetry: true,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LegacyTrackMigrationStatusItem>> GetStatusAsync(
        TrackMasterMigrationStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TrackMasterMigrations.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        var migrations = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.TrackId)
            .Take(Math.Clamp(take <= 0 ? 100 : take, 1, 1_000))
            .ToListAsync(cancellationToken);

        return await CreateStatusItemsAsync(migrations, cancellationToken);
    }

    public async Task<LegacyTrackMigrationCleanupResult> CleanupAsync(
        int retentionDays,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, retentionDays));
        var migrations = await _dbContext.TrackMasterMigrations
            .Where(x => x.Status == TrackMasterMigrationStatus.Completed &&
                x.CompletedAt <= cutoff &&
                x.LegacyDeletedAt == null)
            .OrderBy(x => x.CompletedAt)
            .Take(NormalizeBatchSize(batchSize))
            .ToListAsync(cancellationToken);
        var deleted = 0;
        var failed = 0;

        foreach (var migration in migrations)
        {
            try
            {
                var currentObjectKey = await _dbContext.Tracks
                    .AsNoTracking()
                    .Where(x => x.Id == migration.TrackId)
                    .Select(x => x.TrackPath)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.Equals(currentObjectKey, migration.TargetObjectKey, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Track no longer points at the migrated master object.");
                }

                if (migration.TargetObjectKey is null ||
                    await _mediaStorage.GetMetadataAsync(migration.TargetObjectKey, cancellationToken) is null)
                {
                    throw new InvalidOperationException("Migrated master object could not be verified.");
                }

                var legacyPath = ResolveLegacyPath(migration.LegacyRelativePath);
                if (File.Exists(legacyPath))
                {
                    File.Delete(legacyPath);
                }

                migration.MarkLegacyDeleted(DateTime.UtcNow);
                await _dbContext.SaveChangesAsync(cancellationToken);
                deleted++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
            }
        }

        return new LegacyTrackMigrationCleanupResult(migrations.Count, deleted, failed);
    }

    private async Task<LegacyTrackMigrationRunResult> ProcessMigrationsAsync(
        IReadOnlyCollection<Guid> migrationIds,
        bool queueAnalysis,
        bool queueStems,
        bool forceFailedJobRetry,
        CancellationToken cancellationToken)
    {
        var processed = new List<TrackMasterMigration>();

        foreach (var migrationId in migrationIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var migration = await _dbContext.TrackMasterMigrations
                .FirstAsync(x => x.Id == migrationId, cancellationToken);

            try
            {
                await ProcessOneAsync(
                    migration,
                    queueAnalysis,
                    queueStems,
                    forceFailedJobRetry,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                migration.MarkFailed(ex.Message, DateTime.UtcNow);
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }

            processed.Add(migration);
        }

        var items = await CreateStatusItemsAsync(processed, cancellationToken);
        return new LegacyTrackMigrationRunResult(
            migrationIds.Count,
            processed.Count,
            processed.Count(x => x.Status == TrackMasterMigrationStatus.Completed),
            processed.Count(x => x.Status == TrackMasterMigrationStatus.JobsQueued),
            processed.Count(x => x.Status == TrackMasterMigrationStatus.Failed),
            items);
    }

    private async Task ProcessOneAsync(
        TrackMasterMigration migration,
        bool queueAnalysis,
        bool queueStems,
        bool forceFailedJobRetry,
        CancellationToken cancellationToken)
    {
        var track = await _dbContext.Tracks
            .AsNoTracking()
            .Where(x => x.Id == migration.TrackId)
            .Select(x => new TrackProjection(x.Id, x.TrackPath, x.Length))
            .FirstOrDefaultAsync(cancellationToken) ??
            throw new KeyNotFoundException("Track was not found.");

        if (migration.TargetObjectKey is null ||
            await _mediaStorage.GetMetadataAsync(migration.TargetObjectKey, cancellationToken) is null)
        {
            await UploadAndVerifyAsync(migration, cancellationToken);
        }
        else
        {
            await VerifyObjectAsync(migration, cancellationToken);
        }

        var targetObjectKey = migration.TargetObjectKey!;
        if (!string.Equals(track.TrackPath, targetObjectKey, StringComparison.Ordinal))
        {
            var durationSeconds = track.Length > 0
                ? track.Length
                : await ReadDurationSecondsAsync(migration, cancellationToken);
            var affectedRows = await _dbContext.Tracks
                .Where(x => x.Id == migration.TrackId &&
                    x.TrackPath == migration.LegacyRelativePath)
                .ExecuteUpdateAsync(
                    updates => updates
                        .SetProperty(x => x.TrackPath, targetObjectKey)
                        .SetProperty(x => x.Length, durationSeconds),
                    cancellationToken);
            if (affectedRows == 0)
            {
                throw new InvalidOperationException("Track master changed while the migration was running.");
            }
        }

        migration.MarkDatabaseUpdated(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var analysisId = queueAnalysis
            ? await EnsureAnalysisAsync(migration, forceFailedJobRetry, cancellationToken)
            : migration.AnalysisId;
        var stemSetId = queueStems
            ? await EnsureStemSetAsync(migration, forceFailedJobRetry, cancellationToken)
            : migration.StemSetId;

        migration.RecordJobs(analysisId, stemSetId, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var analysisReady = !queueAnalysis || await IsAnalysisReadyAsync(analysisId, cancellationToken);
        var stemsReady = !queueStems || await IsStemSetReadyAsync(stemSetId, cancellationToken);
        if (analysisReady && stemsReady)
        {
            migration.MarkCompleted(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task UploadAndVerifyAsync(
        TrackMasterMigration migration,
        CancellationToken cancellationToken)
    {
        var sourcePath = ResolveLegacyPath(migration.LegacyRelativePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Legacy master file was not found.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Legacy master file type is unsupported.");
        }

        await using var content = File.OpenRead(sourcePath);
        var checksum = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken))
            .ToLowerInvariant();
        var objectKey = $"{NormalizeObjectPrefix()}/{migration.TrackId}/masters/{checksum}{extension}";
        var contentType = GetContentType(extension);
        migration.MarkUploading(objectKey, content.Length, checksum, contentType, DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);

        content.Position = 0;
        await _mediaStorage.UploadAsync(
            new UploadMediaObject(
                objectKey,
                content,
                contentType,
                new Dictionary<string, string>
                {
                    ["sha256"] = checksum,
                    ["track-id"] = migration.TrackId.ToString()
                }),
            cancellationToken);

        await VerifyObjectAsync(migration, cancellationToken);
    }

    private async Task VerifyObjectAsync(
        TrackMasterMigration migration,
        CancellationToken cancellationToken)
    {
        if (migration.TargetObjectKey is null)
        {
            throw new InvalidOperationException("Migration has no target object key.");
        }

        var metadata = await _mediaStorage.GetMetadataAsync(
            migration.TargetObjectKey,
            cancellationToken) ?? throw new InvalidOperationException("Uploaded master object was not found.");
        if (migration.SourceSizeBytes is not null && metadata.SizeInBytes != migration.SourceSizeBytes)
        {
            throw new InvalidOperationException("Uploaded master object size does not match the source file.");
        }

        if (migration.SourceChecksumSha256 is not null &&
            (!metadata.Metadata.TryGetValue("sha256", out var storedChecksum) ||
                !storedChecksum.Equals(migration.SourceChecksumSha256, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Uploaded master object checksum metadata does not match.");
        }

        migration.MarkObjectVerified(DateTime.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ReadDurationSecondsAsync(
        TrackMasterMigration migration,
        CancellationToken cancellationToken)
    {
        var sourcePath = ResolveLegacyPath(migration.LegacyRelativePath);
        await using var content = File.OpenRead(sourcePath);
        var metadata = await _audioMetadataReader.ReadAsync(
            content,
            Path.GetFileName(sourcePath),
            migration.ContentType ?? "application/octet-stream",
            cancellationToken);

        return metadata.Duration <= TimeSpan.Zero
            ? 0
            : Math.Max(1, (int)Math.Round(metadata.Duration.TotalSeconds, MidpointRounding.AwayFromZero));
    }

    private async Task<Guid?> EnsureAnalysisAsync(
        TrackMasterMigration migration,
        bool forceFailedJobRetry,
        CancellationToken cancellationToken)
    {
        if (migration.AnalysisId is not null)
        {
            var current = await _dbContext.TrackAudioAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == migration.AnalysisId, cancellationToken);
            if (current is not null && current.Status != AudioAnalysisStatus.Failed)
            {
                return current.Id;
            }

            if (current?.Status == AudioAnalysisStatus.Failed && !forceFailedJobRetry)
            {
                throw new InvalidOperationException("Audio analysis failed. Use the retry endpoint to queue it again.");
            }
        }

        var existing = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Where(x => x.TrackId == migration.TrackId &&
                x.ProviderName == _audioOptions.DefaultProvider &&
                x.ModelName == _audioOptions.DefaultModelName &&
                x.ModelVersion == _audioOptions.DefaultModelVersion &&
                x.Status != AudioAnalysisStatus.Failed)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var job = await _audioAnalysisService.StartAsync(
            migration.TrackId,
            "legacy-track-migration",
            cancellationToken);
        return job.JobId;
    }

    private async Task<Guid?> EnsureStemSetAsync(
        TrackMasterMigration migration,
        bool forceFailedJobRetry,
        CancellationToken cancellationToken)
    {
        if (migration.StemSetId is not null)
        {
            var current = await _dbContext.TrackStemSets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == migration.StemSetId, cancellationToken);
            if (current is not null && current.Status != StemSetStatus.Failed)
            {
                return current.Id;
            }

            if (current?.Status == StemSetStatus.Failed && !forceFailedJobRetry)
            {
                throw new InvalidOperationException("Stem separation failed. Use the retry endpoint to queue it again.");
            }
        }

        var existing = await _dbContext.TrackStemSets
            .AsNoTracking()
            .Where(x => x.TrackId == migration.TrackId &&
                x.Source == StemSetSource.AiGenerated &&
                x.ProviderName == _stemOptions.DefaultProvider &&
                x.ModelName == _stemOptions.DefaultModelName &&
                x.ModelVersion == _stemOptions.DefaultModelVersion &&
                x.StemProfile == _stemOptions.DefaultStemProfile &&
                x.Status != StemSetStatus.Failed)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var job = await _stemSeparationService.StartAsync(
            migration.TrackId,
            "legacy-track-migration",
            cancellationToken);
        return job.JobId;
    }

    private async Task<bool> IsAnalysisReadyAsync(Guid? analysisId, CancellationToken cancellationToken)
    {
        return analysisId is not null && await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .AnyAsync(x => x.Id == analysisId && x.Status == AudioAnalysisStatus.Ready, cancellationToken);
    }

    private async Task<bool> IsStemSetReadyAsync(Guid? stemSetId, CancellationToken cancellationToken)
    {
        return stemSetId is not null && await _dbContext.TrackStemSets
            .AsNoTracking()
            .AnyAsync(x => x.Id == stemSetId && x.Status == StemSetStatus.Ready, cancellationToken);
    }

    private async Task<IReadOnlyList<LegacyTrackMigrationStatusItem>> CreateStatusItemsAsync(
        IReadOnlyCollection<TrackMasterMigration> migrations,
        CancellationToken cancellationToken)
    {
        var analysisIds = migrations.Where(x => x.AnalysisId is not null).Select(x => x.AnalysisId!.Value).ToArray();
        var stemSetIds = migrations.Where(x => x.StemSetId is not null).Select(x => x.StemSetId!.Value).ToArray();
        var analysisStatuses = await _dbContext.TrackAudioAnalyses
            .AsNoTracking()
            .Where(x => analysisIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Status.ToString(), cancellationToken);
        var stemStatuses = await _dbContext.TrackStemSets
            .AsNoTracking()
            .Where(x => stemSetIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Status.ToString(), cancellationToken);

        return migrations.Select(x => new LegacyTrackMigrationStatusItem(
            x.Id,
            x.TrackId,
            x.LegacyRelativePath,
            x.TargetObjectKey,
            x.Status.ToString(),
            x.AnalysisId,
            x.AnalysisId is null ? null : analysisStatuses.GetValueOrDefault(x.AnalysisId.Value),
            x.StemSetId,
            x.StemSetId is null ? null : stemStatuses.GetValueOrDefault(x.StemSetId.Value),
            x.AttemptCount,
            x.LastError,
            x.UpdatedAt,
            x.CompletedAt,
            x.LegacyDeletedAt)).ToArray();
    }

    private int NormalizeBatchSize(int batchSize)
    {
        var defaultSize = Math.Max(1, _options.DefaultBatchSize);
        return Math.Clamp(batchSize <= 0 ? defaultSize : batchSize, 1, Math.Max(1, _options.MaxBatchSize));
    }

    private bool IsObjectStorageKey(string path)
    {
        return path.Replace('\\', '/').StartsWith(
            $"{NormalizeObjectPrefix()}/",
            StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeObjectPrefix()
    {
        return string.IsNullOrWhiteSpace(_options.ObjectKeyPrefix)
            ? "tracks"
            : _options.ObjectKeyPrefix.Trim().Trim('/', '\\');
    }

    private string ResolveLegacyPath(string relativePath)
    {
        var root = _legacyTrackRoot;
        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Legacy track path escapes the configured migration root.");
        }

        return fullPath;
    }

    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };
    }

    private sealed record TrackProjection(int Id, string TrackPath, int Length);
}
