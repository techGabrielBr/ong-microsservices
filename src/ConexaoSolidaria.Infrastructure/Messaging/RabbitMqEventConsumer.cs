using System.Text;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class RabbitMqEventConsumer(
    RabbitMqConnectionProvider provider,
    IOptions<MessagingOptions> options,
    ILogger<RabbitMqEventConsumer> logger) : IEventConsumer
{
    private readonly RabbitMqOptions _options = options.Value.RabbitMq;

    public async Task ConsumirAsync(Func<string, CancellationToken, Task> handler, CancellationToken ct)
    {
        var connection = await provider.ObterConexaoAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var payload = Encoding.UTF8.GetString(ea.Body.Span);

            try
            {
                await handler(payload, ct);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar mensagem {DeliveryTag}; encaminhando para DLQ.", ea.DeliveryTag);
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        };

        await channel.BasicConsumeAsync(
            queue: MessagingTopics.FilaDoacoesRecebidas,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        logger.LogInformation("Consumidor RabbitMQ escutando a fila {Fila}.", MessagingTopics.FilaDoacoesRecebidas);

        await Task.Delay(Timeout.Infinite, ct).ContinueWith(_ => { }, TaskScheduler.Default);
    }
}
