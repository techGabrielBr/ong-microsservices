using ConexaoSolidaria.Contracts.Events;
using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Infrastructure.Ledger;
using ConexaoSolidaria.Infrastructure.Messaging;
using ConexaoSolidaria.Infrastructure.Observabilidade;
using ConexaoSolidaria.Infrastructure.Persistence;

namespace ConexaoSolidaria.Api.Services;

public sealed record ResultadoDoacao(bool Sucesso, Doacao? Doacao, string? Erro, int StatusCode);

/// A API apenas registra a intencao e publica o evento; a consolidacao do valor
/// arrecadado e responsabilidade exclusiva do Worker.
public sealed class DoacaoService(
    ICampanhaRepository campanhas,
    IDoacaoLedger ledger,
    IEventPublisher publisher,
    ILogger<DoacaoService> logger)
{
    public async Task<ResultadoDoacao> RegistrarIntencaoAsync(
        Guid campanhaId,
        Guid doadorId,
        decimal valor,
        CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var campanha = await campanhas.ObterPorIdAsync(campanhaId, ct);

        if (campanha is null)
        {
            MetricasDoacao.Rejeitadas.WithLabels("campanha_inexistente").Inc();
            return new ResultadoDoacao(false, null, "Campanha nao encontrada.", StatusCodes.Status404NotFound);
        }

        if (!campanha.PodeReceberDoacao(agora))
        {
            MetricasDoacao.Rejeitadas.WithLabels("campanha_indisponivel").Inc();
            return new ResultadoDoacao(
                false,
                null,
                $"Campanha com status {campanha.Status} ou encerrada em {campanha.DataFim:yyyy-MM-dd} nao aceita doacoes.",
                StatusCodes.Status422UnprocessableEntity);
        }

        var doacao = Doacao.Criar(campanhaId, doadorId, valor, agora);
        await ledger.SalvarAsync(doacao, ct);

        try
        {
            await publisher.PublicarAsync(
                new DoacaoRecebidaEvent
                {
                    DoacaoId = doacao.Id,
                    CampanhaId = doacao.CampanhaId,
                    DoadorId = doacao.DoadorId,
                    Valor = doacao.Valor,
                    OcorridoEm = doacao.CriadaEm
                },
                DoacaoRecebidaEvent.Nome,
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao publicar {Evento} da doacao {DoacaoId}.", DoacaoRecebidaEvent.Nome, doacao.Id);
            await ledger.MarcarComoRejeitadaAsync(campanhaId, doacao.Id, "Falha ao publicar evento no broker.", DateTime.UtcNow, ct);
            MetricasDoacao.Rejeitadas.WithLabels("falha_broker").Inc();

            return new ResultadoDoacao(false, null, "Nao foi possivel registrar a doacao no momento.", StatusCodes.Status503ServiceUnavailable);
        }

        MetricasDoacao.Recebidas.Inc();
        logger.LogInformation("Doacao {DoacaoId} registrada para a campanha {CampanhaId}.", doacao.Id, campanhaId);

        return new ResultadoDoacao(true, doacao, null, StatusCodes.Status202Accepted);
    }
}
