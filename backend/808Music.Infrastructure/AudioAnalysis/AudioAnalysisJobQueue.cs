using _808Music.Application.AudioAnalysis;
using _808Music.Application.Common.Messaging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.AudioAnalysis;

public sealed class AudioAnalysisJobQueue : IAudioAnalysisJobQueue
{
    private readonly IMessagePublisher _publisher;
    private readonly AudioAnalysisOptions _options;

    public AudioAnalysisJobQueue(
        IMessagePublisher publisher,
        IOptions<AudioAnalysisOptions> options)
    {
        _publisher = publisher;
        _options = options.Value;
    }

    public Task EnqueueAsync(
        AudioAnalysisRequestedMessage message,
        CancellationToken cancellationToken = default)
    {
        return _publisher.PublishAsync(
            message,
            _options.RoutingKey,
            _options.QueueName,
            cancellationToken);
    }
}
