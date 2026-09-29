using ConexaoSolidaria.Infrastructure.Messaging;

namespace ConexaoSolidaria.Worker.Processing;

public sealed class ConsumidorDeDoacoes(
    IEventConsumer consumer,
    IServiceScopeFactory scopeFactory,
    ILogger<ConsumidorDeDoacoes> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de doacoes iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await consumer.ConsumirAsync(ProcessarAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Consumidor caiu; reconectando em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        logger.LogInformation("Worker de doacoes encerrado.");
    }

    private async Task ProcessarAsync(string payload, CancellationToken ct)
    {
        using var escopo = scopeFactory.CreateScope();
        var processador = escopo.ServiceProvider.GetRequiredService<ProcessadorDeDoacoes>();

        await processador.ProcessarAsync(payload, ct);
    }
}
