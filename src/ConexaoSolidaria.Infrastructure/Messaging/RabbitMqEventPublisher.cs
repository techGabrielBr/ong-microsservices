using System.Text;
using System.Text.Json;
using ConexaoSolidaria.Contracts.Events;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher(
    RabbitMqConnectionProvider provider,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher
{
    public async Task PublicarAsync<TEvento>(TEvento evento, string nomeEvento, CancellationToken ct = default)
        where TEvento : class
    {
        var connection = await provider.ObterConexaoAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var payload = JsonSerializer.SerializeToUtf8Bytes(evento, JsonPadrao.Options);
        var propriedades = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Type = nomeEvento,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: MessagingTopics.Exchange,
            routingKey: MessagingTopics.RoutingKeyDoacaoRecebida,
            mandatory: false,
            basicProperties: propriedades,
            body: payload,
            cancellationToken: ct);

        logger.LogInformation("Evento {Evento} publicado no RabbitMQ ({Bytes} bytes).", nomeEvento, Encoding.UTF8.GetString(payload).Length);
    }
}
