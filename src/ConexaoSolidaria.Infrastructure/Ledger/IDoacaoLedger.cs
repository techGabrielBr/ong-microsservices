using ConexaoSolidaria.Domain.Entities;

namespace ConexaoSolidaria.Infrastructure.Ledger;

public interface IDoacaoLedger
{
    Task SalvarAsync(Doacao doacao, CancellationToken ct = default);
    Task<Doacao?> ObterAsync(Guid campanhaId, Guid doacaoId, CancellationToken ct = default);
    Task<IReadOnlyList<Doacao>> ListarPorCampanhaAsync(Guid campanhaId, CancellationToken ct = default);
    Task<IReadOnlyList<Doacao>> ListarPorDoadorAsync(Guid doadorId, CancellationToken ct = default);

    /// Transicao Pendente -> Processada com escrita condicional. Retorna false quando a
    /// mensagem ja foi processada antes (entrega duplicada do broker).
    Task<bool> TentarMarcarComoProcessadaAsync(Guid campanhaId, Guid doacaoId, DateTime agoraUtc, CancellationToken ct = default);

    Task MarcarComoRejeitadaAsync(Guid campanhaId, Guid doacaoId, string motivo, DateTime agoraUtc, CancellationToken ct = default);
}
