using _808Music.Application.Abstractions;

namespace _808Music.Application.Stems;

public sealed record SeparateTrackStemsCommand(Guid TrackId, string? RequestedByUserId);

public sealed record SeparateTrackStemsResult(StemSeparationJob Job);

public sealed record GetTrackStemsQuery(Guid TrackId, string? RequestedByUserId);

public sealed record GetTrackStemsResult(
    Guid TrackId,
    IReadOnlyList<StemManifestItem> Stems);

public interface ISeparateTrackStemsHandler
{
    Task<SeparateTrackStemsResult> Handle(
        SeparateTrackStemsCommand command,
        CancellationToken cancellationToken = default);
}

public interface IGetTrackStemsHandler
{
    Task<GetTrackStemsResult> Handle(
        GetTrackStemsQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class SeparateTrackStemsHandler : ISeparateTrackStemsHandler
{
    private readonly IStemSeparationService _stemSeparationService;

    public SeparateTrackStemsHandler(IStemSeparationService stemSeparationService)
    {
        _stemSeparationService = stemSeparationService;
    }

    public async Task<SeparateTrackStemsResult> Handle(
        SeparateTrackStemsCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _stemSeparationService.StartAsync(
            command.TrackId,
            command.RequestedByUserId,
            cancellationToken);

        return new SeparateTrackStemsResult(job);
    }
}

public sealed class GetTrackStemsHandler : IGetTrackStemsHandler
{
    private readonly IStemSeparationService _stemSeparationService;

    public GetTrackStemsHandler(IStemSeparationService stemSeparationService)
    {
        _stemSeparationService = stemSeparationService;
    }

    public async Task<GetTrackStemsResult> Handle(
        GetTrackStemsQuery query,
        CancellationToken cancellationToken = default)
    {
        var stems = await _stemSeparationService.GetManifestAsync(query.TrackId, cancellationToken);

        return new GetTrackStemsResult(query.TrackId, stems);
    }
}
