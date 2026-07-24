using _808Music.Application.Abstractions;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace _808Music.Application.Stems;

public sealed record UploadManualStemSetCommand(
    int TrackId,
    string StemProfile,
    IReadOnlyList<ManualStemUpload> Stems);

public sealed record ManualStemUpload(
    StemType StemType,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record UploadManualStemSetResult(
    Guid StemSetId,
    int TrackId,
    string Status);

public interface IUploadManualStemSetHandler
{
    Task<UploadManualStemSetResult> Handle(
        UploadManualStemSetCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class UploadManualStemSetHandler : IUploadManualStemSetHandler
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav",
        "audio/wave",
        "audio/x-wav",
        "audio/mpeg",
        "audio/mp3",
        "audio/flac",
        "audio/aiff",
        "audio/x-aiff"
    };

    private readonly IApplicationDbContext _dbContext;
    private readonly IMediaStorage _mediaStorage;

    public UploadManualStemSetHandler(
        IApplicationDbContext dbContext,
        IMediaStorage mediaStorage)
    {
        _dbContext = dbContext;
        _mediaStorage = mediaStorage;
    }

    public async Task<UploadManualStemSetResult> Handle(
        UploadManualStemSetCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.TrackId <= 0)
        {
            throw new InvalidOperationException("Track id is required.");
        }

        if (!await _dbContext.Tracks.AnyAsync(x => x.Id == command.TrackId, cancellationToken))
        {
            throw new KeyNotFoundException("Track was not found.");
        }

        var stemProfile = NormalizeStemProfile(command.StemProfile);
        ValidateStems(stemProfile, command.Stems);

        var stemSet = new TrackStemSet(
            command.TrackId,
            StemSetSource.ArtistUploaded,
            requestedByUserId: null,
            "artist-upload",
            modelName: null,
            modelVersion: null,
            stemProfile);

        stemSet.MarkProcessing();

        var uploadedObjectKeys = new List<string>();

        try
        {
            foreach (var stem in command.Stems)
            {
                var extension = GetExtension(stem.FileName, stem.ContentType);
                var objectKey = $"stems/{command.TrackId}/{stemSet.Id}/{NormalizeStemName(stem.StemType)}{extension}";
                var checksum = await ComputeChecksumSha256(stem.Content, cancellationToken);

                if (stem.Content.CanSeek)
                {
                    stem.Content.Position = 0;
                }

                var storedObject = await _mediaStorage.UploadAsync(
                    new UploadMediaObject(
                        objectKey,
                        stem.Content,
                        stem.ContentType),
                    cancellationToken);

                uploadedObjectKeys.Add(storedObject.ObjectKey);

                stemSet.AddStem(new TrackStem(
                    stemSet.Id,
                    stem.StemType,
                    "s3",
                    storedObject.ObjectKey,
                    storedObject.ContentType,
                    storedObject.SizeInBytes ?? stem.SizeBytes,
                    durationMs: null,
                    sampleRate: null,
                    bitrateKbps: null,
                    codec: extension.TrimStart('.'),
                    channels: null,
                    checksum));
            }

            stemSet.MarkReady();
            stemSet.Activate();

            var dbContext = _dbContext as DbContext ??
                throw new InvalidOperationException("Manual stem upload requires an EF Core DbContext.");

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.TrackStemSets
                .Where(x => x.TrackId == command.TrackId && x.IsActive)
                .ExecuteUpdateAsync(
                    updates => updates.SetProperty(x => x.IsActive, false),
                    cancellationToken);

            await _dbContext.TrackStemSets.AddAsync(stemSet, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new UploadManualStemSetResult(
                stemSet.Id,
                stemSet.TrackId,
                stemSet.Status.ToString());
        }
        catch
        {
            foreach (var objectKey in uploadedObjectKeys)
            {
                await _mediaStorage.DeleteAsync(objectKey, CancellationToken.None);
            }

            throw;
        }
    }

    private static void ValidateStems(
        string stemProfile,
        IReadOnlyCollection<ManualStemUpload> stems)
    {
        if (stems.Count == 0)
        {
            throw new InvalidOperationException("At least one stem file is required.");
        }

        var duplicateStemTypes = stems
            .GroupBy(stem => stem.StemType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateStemTypes.Length != 0)
        {
            throw new InvalidOperationException(
                $"Upload contains duplicate stems: {string.Join(", ", duplicateStemTypes)}.");
        }

        foreach (var stem in stems)
        {
            if (stem.SizeBytes <= 0)
            {
                throw new InvalidOperationException($"{stem.StemType} stem file is empty.");
            }

            if (!AllowedContentTypes.Contains(stem.ContentType))
            {
                throw new InvalidOperationException(
                    $"{stem.StemType} stem content type '{stem.ContentType}' is not supported.");
            }
        }

        var existingStemTypes = stems
            .Select(stem => stem.StemType)
            .ToHashSet();

        var missingStemTypes = GetRequiredStemTypes(stemProfile)
            .Where(stemType => !existingStemTypes.Contains(stemType))
            .ToArray();

        if (missingStemTypes.Length != 0)
        {
            throw new InvalidOperationException(
                $"Upload is missing required stems: {string.Join(", ", missingStemTypes)}.");
        }
    }

    private static string NormalizeStemProfile(string stemProfile)
    {
        return string.IsNullOrWhiteSpace(stemProfile)
            ? "four-stem"
            : stemProfile.Trim();
    }

    private static IReadOnlyCollection<StemType> GetRequiredStemTypes(string stemProfile)
    {
        if (stemProfile.Equals("two-stem-vocals", StringComparison.OrdinalIgnoreCase) ||
            stemProfile.Equals("vocals", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                StemType.Vocals,
                StemType.Instrumental
            ];
        }

        if (!stemProfile.Equals("four-stem", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported stem profile '{stemProfile}'.");
        }

        return
        [
            StemType.Vocals,
            StemType.Drums,
            StemType.Bass,
            StemType.Other
        ];
    }

    private static string NormalizeStemName(StemType stemType)
    {
        return stemType.ToString().ToLowerInvariant();
    }

    private static string GetExtension(
        string fileName,
        string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        return contentType.ToLowerInvariant() switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/flac" => ".flac",
            "audio/aiff" or "audio/x-aiff" => ".aiff",
            _ => ".audio"
        };
    }

    private static async Task<string?> ComputeChecksumSha256(
        Stream stream,
        CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        stream.Position = 0;

        var checksum = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(checksum).ToLowerInvariant();
    }
}
