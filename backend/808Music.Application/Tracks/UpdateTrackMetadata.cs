using _808Music.Application.Common.Persistence;
using _808Music.Domain.Catalog;

namespace _808Music.Application.Tracks;

public sealed record UpdateTrackMetadataCommand(
    int TrackId,
    string Title,
    bool IsExplicit);

public sealed record UpdateTrackMetadataResult(
    int Id,
    string Title,
    bool IsExplicit);

public interface IUpdateTrackMetadataHandler
{
    Task<UpdateTrackMetadataResult?> Handle(
        UpdateTrackMetadataCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateTrackMetadataHandler : IUpdateTrackMetadataHandler
{
    private readonly IRepository<Track, int> _trackRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTrackMetadataHandler(
        IRepository<Track, int> trackRepository,
        IUnitOfWork unitOfWork)
    {
        _trackRepository = trackRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateTrackMetadataResult?> Handle(
        UpdateTrackMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new InvalidOperationException("Track title is required.");
        }

        var track = await _trackRepository.GetByIdAsync(command.TrackId, cancellationToken);
        if (track is null)
        {
            return null;
        }

        track.Title = command.Title.Trim();
        track.IsExplicit = command.IsExplicit;

        _trackRepository.Update(track);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateTrackMetadataResult(
            track.Id,
            track.Title,
            track.IsExplicit);
    }
}
