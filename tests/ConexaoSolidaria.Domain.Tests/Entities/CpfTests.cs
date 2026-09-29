using ConexaoSolidaria.Domain.Exceptions;
using ConexaoSolidaria.Domain.ValueObjects;
using FluentAssertions;

namespace ConexaoSolidaria.Domain.Tests.Entities;

public class CpfTests
{
    [Theory]
    [InlineData("390.533.447-05")]
    [InlineData("39053344705")]
    [InlineData("529.982.247-25")]
    public void Criar_ComCpfValido_RemoveMascara(string entrada)
    {
        var cpf = Cpf.Criar(entrada);

        cpf.Numero.Should().MatchRegex("^[0-9]{11}$");
    }

    [Fact]
    public void Formatado_AplicaMascaraPadrao()
    {
        Cpf.Criar("39053344705").Formatado.Should().Be("390.533.447-05");
    }

    [Theory]
    [InlineData("111.111.111-11")]
    [InlineData("00000000000")]
    [InlineData("390.533.447-06")]
    [InlineData("1234567890")]
    [InlineData("abcdefghijk")]
    [InlineData(null)]
    public void Criar_ComCpfInvalido_Falha(string? entrada)
    {
        var acao = () => Cpf.Criar(entrada);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void TryCriar_ComCpfInvalido_RetornaFalsoSemExcecao()
    {
        Cpf.TryCriar("123", out _).Should().BeFalse();
    }
}
