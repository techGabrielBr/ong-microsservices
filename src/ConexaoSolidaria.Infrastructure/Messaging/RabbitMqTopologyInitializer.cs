using ConexaoSolidaria.Contracts.Events;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    RabbitMqConnectionProvider provider,
    ILogger<RabbitMqTopologyInitializer> logger) : IMessagingTopologyInitializer
{
    public async Task GarantirTopologiaAsync(CancellationToken ct = default)
    {
        var connection = await provider.ObterConexaoAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(MessagingTopics.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(MessagingTopics.FilaDoacoesDlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        await channel.QueueDeclareAsync(
            MessagingTopics.FilaDoacoesRecebidas,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = MessagingTopics.FilaDoacoesDlq
            },
            cancellationToken: ct);

        await channel.QueueBindAsync(
            MessagingTopics.FilaDoacoesRecebidas,
            MessagingTopics.Exchange,
            MessagingTopics.RoutingKeyDoacaoRecebida,
            cancellationToken: ct);

        logger.LogInformation("Topologia RabbitMQ garantida: exchange {Exchange}, fila {Fila}.", MessagingTopics.Exchange, MessagingTopics.FilaDoacoesRecebidas);
    }
}
