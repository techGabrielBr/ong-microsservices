using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;
using FluentAssertions;

namespace ConexaoSolidaria.Domain.Tests.Entities;

public class UsuarioTests
{
    private static readonly DateTime Agora = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criar_DoadorValido_NormalizaEmailECpf()
    {
        var usuario = Usuario.Criar("Maria Souza", "  MARIA@Exemplo.COM ", "390.533.447-05", "hash", PerfilUsuario.Doador, Agora);

        usuario.Email.Should().Be("maria@exemplo.com");
        usuario.Cpf.Should().Be("39053344705");
        usuario.Perfil.Should().Be(PerfilUsuario.Doador);
        usuario.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Criar_SemSobrenome_Falha()
    {
        var acao = () => Usuario.Criar("Maria", "maria@exemplo.com", "39053344705", "hash", PerfilUsuario.Doador, Agora);

        acao.Should().Throw<DomainException>().WithMessage("*nome e sobrenome*");
    }

    [Theory]
    [InlineData("maria.exemplo.com")]
    [InlineData("maria@")]
    [InlineData("")]
    public void Criar_ComEmailInvalido_Falha(string email)
    {
        var acao = () => Usuario.Criar("Maria Souza", email, "39053344705", "hash", PerfilUsuario.Doador, Agora);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Desativar_MarcaUsuarioComoInativo()
    {
        var usuario = Usuario.Criar("Joao Lima", "joao@exemplo.com", "39053344705", "hash", PerfilUsuario.GestorONG, Agora);

        usuario.Desativar();

        usuario.Ativo.Should().BeFalse();
    }
}
