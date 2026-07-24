using _808Music.Application.Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace _808Music.Infrastructure.Messaging;

public sealed class RabbitMqMessagePublisher : IMessagePublisher
{
    private readonly RabbitMqOptions _options;

    public RabbitMqMessagePublisher(IOptions<RabbitMqOptions> options)
    {
        _options = options.Value;
    }

    public Task PublishAsync<TMessage>(
        TMessage message,
        string routingKey,
        string? queueName = null,
        CancellationToken cancellationToken = default)
        where TMessage : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);

        if (!string.IsNullOrWhiteSpace(queueName))
        {
            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            channel.QueueBind(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: routingKey);
        }

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = Guid.NewGuid().ToString("N");
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }
}
