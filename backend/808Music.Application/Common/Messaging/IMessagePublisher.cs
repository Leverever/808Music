namespace _808Music.Application.Common.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<TMessage>(
        TMessage message,
        string routingKey,
        string? queueName = null,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
