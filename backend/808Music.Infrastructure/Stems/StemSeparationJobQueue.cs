using _808Music.Application.Common.Messaging;
using _808Music.Application.Stems;
using Microsoft.Extensions.Options;

namespace _808Music.Infrastructure.Stems;

public sealed class StemSeparationJobQueue : IStemSeparationJobQueue
{
    private readonly IMessagePublisher _publisher;
    private readonly StemSeparationOptions _options;

    public StemSeparationJobQueue(
        IMessagePublisher publisher,
        IOptions<StemSeparationOptions> options)
    {
        _publisher = publisher;
        _options = options.Value;
    }

    public Task EnqueueAsync(
        StemSeparationRequestedMessage message,
        CancellationToken cancellationToken = default)
    {
        return _publisher.PublishAsync(
            message,
            _options.RoutingKey,
            _options.QueueName,
            cancellationToken);
    }
}
