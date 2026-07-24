using _808Music.Application.AudioClustering;
using _808Music.Application.Common.Messaging;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.AudioClustering;

public sealed class AudioClusteringJobQueue : IAudioClusteringJobQueue
{
    private readonly IMessagePublisher _publisher;
    private readonly AudioClusteringOptions _options;

    public AudioClusteringJobQueue(
        IMessagePublisher publisher,
        IOptions<AudioClusteringOptions> options)
    {
        _publisher = publisher;
        _options = options.Value;
    }

    public Task EnqueueAsync(
        AudioClusteringRequestedMessage message,
        CancellationToken cancellationToken = default)
    {
        return _publisher.PublishAsync(
            message,
            _options.RoutingKey,
            _options.QueueName,
            cancellationToken);
    }
}
