using ConexaoSolidaria.Domain.Entities;

namespace ConexaoSolidaria.Infrastructure.Persistence;

public interface ICampanhaRepository
{
    Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Campanha>> ListarTodasAsync(CancellationToken ct = default);
    Task AdicionarAsync(Campanha campanha, CancellationToken ct = default);
    Task<int> SalvarAsync(CancellationToken ct = default);

    /// Incremento atomico executado pelo Worker; evita perda de escrita concorrente.
    Task<bool> IncrementarArrecadadoAsync(Guid campanhaId, decimal valor, CancellationToken ct = default);
}
