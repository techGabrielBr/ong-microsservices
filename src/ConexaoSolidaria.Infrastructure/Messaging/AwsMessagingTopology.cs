using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure.Messaging;

/// Provisiona SNS + SQS (com DLQ e redrive) contra LocalStack ou AWS real.
public sealed class AwsMessagingTopology(
    IAmazonSimpleNotificationService sns,
    IAmazonSQS sqs,
    IOptions<MessagingOptions> options,
    ILogger<AwsMessagingTopology> logger) : IMessagingTopologyInitializer
{
    private readonly AwsMessagingOptions _options = options.Value.Aws;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _topicArn;
    private string? _queueUrl;

    public async Task GarantirTopologiaAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_topicArn is not null && _queueUrl is not null)
            {
                return;
            }

            var topico = await sns.CreateTopicAsync(_options.TopicName, ct);
            var dlq = await sqs.CreateQueueAsync(_options.DeadLetterQueueName, ct);
            var dlqArn = await ObterAtributoAsync(dlq.QueueUrl, QueueAttributeName.QueueArn, ct);

            var fila = await sqs.CreateQueueAsync(
                new CreateQueueRequest
                {
                    QueueName = _options.QueueName,
                    Attributes = new Dictionary<string, string>
                    {
                        [QueueAttributeName.RedrivePolicy] = JsonSerializer.Serialize(new
                        {
                            deadLetterTargetArn = dlqArn,
                            maxReceiveCount = "5"
                        }),
                        [QueueAttributeName.VisibilityTimeout] = "60"
                    }
                },
                ct);

            var filaArn = await ObterAtributoAsync(fila.QueueUrl, QueueAttributeName.QueueArn, ct);

            var assinatura = await sns.SubscribeAsync(
                new SubscribeRequest
                {
                    TopicArn = topico.TopicArn,
                    Protocol = "sqs",
                    Endpoint = filaArn,
                    ReturnSubscriptionArn = true,
                    Attributes = new Dictionary<string, string> { ["RawMessageDelivery"] = "true" }
                },
                ct);

            _topicArn = topico.TopicArn;
            _queueUrl = fila.QueueUrl;

            logger.LogInformation(
                "Topologia AWS garantida. Topico {Topico}, fila {Fila}, assinatura {Assinatura}.",
                _topicArn,
                _queueUrl,
                assinatura.SubscriptionArn);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> ObterTopicArnAsync(CancellationToken ct = default)
    {
        await GarantirTopologiaAsync(ct);
        return _topicArn!;
    }

    public async Task<string> ObterQueueUrlAsync(CancellationToken ct = default)
    {
        await GarantirTopologiaAsync(ct);
        return _queueUrl!;
    }

    private async Task<string> ObterAtributoAsync(string queueUrl, string atributo, CancellationToken ct)
    {
        var resposta = await sqs.GetQueueAttributesAsync(queueUrl, [atributo], ct);
        return resposta.Attributes[atributo];
    }
}
