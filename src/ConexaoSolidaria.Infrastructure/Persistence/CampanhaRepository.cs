using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Infrastructure.Persistence;

public sealed class CampanhaRepository(ConexaoSolidariaDbContext db) : ICampanhaRepository
{
    public Task<Campanha?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        db.Campanhas.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Campanha>> ListarAtivasAsync(CancellationToken ct = default) =>
        await db.Campanhas
            .AsNoTracking()
            .Where(c => c.Status == StatusCampanha.Ativa)
            .OrderByDescending(c => c.CriadaEm)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Campanha>> ListarTodasAsync(CancellationToken ct = default) =>
        await db.Campanhas
            .AsNoTracking()
            .OrderByDescending(c => c.CriadaEm)
            .ToListAsync(ct);

    public async Task AdicionarAsync(Campanha campanha, CancellationToken ct = default) =>
        await db.Campanhas.AddAsync(campanha, ct);

    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<bool> IncrementarArrecadadoAsync(Guid campanhaId, decimal valor, CancellationToken ct = default)
    {
        var afetadas = await db.Campanhas
            .Where(c => c.Id == campanhaId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.ValorArrecadado, c => c.ValorArrecadado + valor)
                    .SetProperty(c => c.AtualizadaEm, DateTime.UtcNow),
                ct);

        return afetadas > 0;
    }
}
