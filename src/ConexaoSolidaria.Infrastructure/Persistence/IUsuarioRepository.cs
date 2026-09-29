using ConexaoSolidaria.Domain.Entities;

namespace ConexaoSolidaria.Infrastructure.Persistence;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExisteCpfAsync(string cpf, CancellationToken ct = default);
    Task AdicionarAsync(Usuario usuario, CancellationToken ct = default);
    Task<int> SalvarAsync(CancellationToken ct = default);
}
