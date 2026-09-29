using System.Diagnostics;
using System.Text.Json;
using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Infrastructure.Ledger;
using ConexaoSolidaria.Infrastructure.Messaging;
using ConexaoSolidaria.Infrastructure.Observabilidade;
using ConexaoSolidaria.Infrastructure.Persistence;

namespace ConexaoSolidaria.Worker.Processing;

public sealed class ProcessadorDeDoacoes(
    ICampanhaRepository campanhas,
    IDoacaoLedger ledger,
    ILogger<ProcessadorDeDoacoes> logger)
{
    public async Task ProcessarAsync(string payload, CancellationToken ct)
    {
        var evento = Desserializar(payload);
        var cronometro = Stopwatch.StartNew();

        try
        {
            var agora = DateTime.UtcNow;
            var podeConsolidar = await ledger.TentarMarcarComoProcessadaAsync(evento.CampanhaId, evento.DoacaoId, agora, ct);

            if (!podeConsolidar)
            {
                MetricasDoacao.Duplicadas.Inc();
                logger.LogInformation("Doacao {DoacaoId} ja processada anteriormente; mensagem descartada.", evento.DoacaoId);
                return;
            }

            var atualizada = await campanhas.IncrementarArrecadadoAsync(evento.CampanhaId, evento.Valor, ct);

            if (!atualizada)
            {
                await ledger.MarcarComoRejeitadaAsync(evento.CampanhaId, evento.DoacaoId, "Campanha inexistente na consolidacao.", DateTime.UtcNow, ct);
                throw new InvalidOperationException($"Campanha {evento.CampanhaId} nao encontrada para consolidacao.");
            }

            MetricasDoacao.Processadas.Inc();
            MetricasDoacao.ValorArrecadadoTotal.Inc((double)evento.Valor);

            logger.LogInformation(
                "Doacao {DoacaoId} de {Valor:C} consolidada na campanha {CampanhaId}.",
                evento.DoacaoId,
                evento.Valor,
                evento.CampanhaId);
        }
        catch
        {
            MetricasDoacao.FalhasProcessamento.Inc();
            throw;
        }
        finally
        {
            MetricasDoacao.DuracaoProcessamento.Observe(cronometro.Elapsed.TotalSeconds);
        }
    }

    /// Mensagem malformada precisa falhar aqui e seguir para a DLQ; sem esta checagem
    /// um payload vazio desserializa em um evento com Guids vazios e seria descartado
    /// silenciosamente como se fosse uma reentrega.
    private static DoacaoRecebidaEvent Desserializar(string payload)
    {
        DoacaoRecebidaEvent? evento;

        try
        {
            evento = JsonSerializer.Deserialize<DoacaoRecebidaEvent>(payload, JsonPadrao.Options);
        }
        catch (JsonException ex)
        {
            MetricasDoacao.FalhasProcessamento.Inc();
            throw new InvalidOperationException("Payload nao e um JSON valido.", ex);
        }

        if (evento is null || evento.DoacaoId == Guid.Empty || evento.CampanhaId == Guid.Empty || evento.Valor <= 0)
        {
            MetricasDoacao.FalhasProcessamento.Inc();
            throw new InvalidOperationException($"Payload nao corresponde a um {DoacaoRecebidaEvent.Nome}: {payload}");
        }

        return evento;
    }
}
