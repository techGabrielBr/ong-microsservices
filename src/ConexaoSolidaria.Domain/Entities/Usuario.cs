using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;
using ConexaoSolidaria.Domain.ValueObjects;

namespace ConexaoSolidaria.Domain.Entities;

public sealed class Usuario
{
    private Usuario()
    {
        NomeCompleto = string.Empty;
        Email = string.Empty;
        Cpf = string.Empty;
        SenhaHash = string.Empty;
    }

    public Guid Id { get; private set; }
    public string NomeCompleto { get; private set; }
    public string Email { get; private set; }
    public string Cpf { get; private set; }
    public string SenhaHash { get; private set; }
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public DateTime CriadoEm { get; private set; }

    public static Usuario Criar(
        string nomeCompleto,
        string email,
        string cpf,
        string senhaHash,
        PerfilUsuario perfil,
        DateTime agoraUtc)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(nomeCompleto), "Nome completo e obrigatorio.");
        DomainException.Require(nomeCompleto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2, "Informe nome e sobrenome.");
        DomainException.Require(!string.IsNullOrWhiteSpace(senhaHash), "Hash de senha e obrigatorio.");

        return new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = nomeCompleto.Trim(),
            Email = ValueObjects.Email.Criar(email).Endereco,
            Cpf = ValueObjects.Cpf.Criar(cpf).Numero,
            SenhaHash = senhaHash,
            Perfil = perfil,
            Ativo = true,
            CriadoEm = agoraUtc
        };
    }

    public void Desativar() => Ativo = false;
}
