using ConexaoSolidaria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConexaoSolidaria.Infrastructure.Persistence;

public sealed class UsuarioRepository(ConexaoSolidariaDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower(), ct);

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Email == email.Trim().ToLower(), ct);

    public Task<bool> ExisteCpfAsync(string cpf, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Cpf == cpf, ct);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct = default) =>
        await db.Usuarios.AddAsync(usuario, ct);

    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
