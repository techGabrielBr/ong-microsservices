using Amazon.SQS;
using Amazon.SQS.Model;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class AwsEventConsumer(
    IAmazonSQS sqs,
    AwsMessagingTopology topologia,
    IOptions<MessagingOptions> options,
    ILogger<AwsEventConsumer> logger) : IEventConsumer
{
    private readonly AwsMessagingOptions _options = options.Value.Aws;

    public async Task ConsumirAsync(Func<string, CancellationToken, Task> handler, CancellationToken ct)
    {
        var queueUrl = await topologia.ObterQueueUrlAsync(ct);
        logger.LogInformation("Consumidor SQS escutando {Fila}.", queueUrl);

        while (!ct.IsCancellationRequested)
        {
            var resposta = await sqs.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = _options.MaxNumberOfMessages,
                    WaitTimeSeconds = _options.WaitTimeSeconds
                },
                ct);

            foreach (var mensagem in resposta.Messages ?? [])
            {
                try
                {
                    await handler(mensagem.Body, ct);
                    await sqs.DeleteMessageAsync(queueUrl, mensagem.ReceiptHandle, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falha ao processar mensagem {MessageId}; sera reentregue ate a DLQ.", mensagem.MessageId);
                }
            }
        }
    }
}
