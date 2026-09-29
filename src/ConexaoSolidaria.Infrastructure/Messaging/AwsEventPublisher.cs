using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Microsoft.Extensions.Logging;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class AwsEventPublisher(
    IAmazonSimpleNotificationService sns,
    AwsMessagingTopology topologia,
    ILogger<AwsEventPublisher> logger) : IEventPublisher
{
    public async Task PublicarAsync<TEvento>(TEvento evento, string nomeEvento, CancellationToken ct = default)
        where TEvento : class
    {
        var topicArn = await topologia.ObterTopicArnAsync(ct);

        var resposta = await sns.PublishAsync(
            new PublishRequest
            {
                TopicArn = topicArn,
                Message = JsonSerializer.Serialize(evento, JsonPadrao.Options),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["eventName"] = new() { DataType = "String", StringValue = nomeEvento }
                }
            },
            ct);

        logger.LogInformation("Evento {Evento} publicado no SNS ({MessageId}).", nomeEvento, resposta.MessageId);
    }
}
