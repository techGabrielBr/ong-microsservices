using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;
using FluentAssertions;

namespace ConexaoSolidaria.Domain.Tests.Entities;

public class DoacaoTests
{
    private static readonly DateTime Agora = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criar_NasceComoPendente()
    {
        var doacao = Doacao.Criar(Guid.NewGuid(), Guid.NewGuid(), 150.505m, Agora);

        doacao.Status.Should().Be(StatusDoacao.Pendente);
        doacao.Valor.Should().Be(150.51m);
        doacao.ProcessadaEm.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_ComValorNaoPositivo_Falha(decimal valor)
    {
        var acao = () => Doacao.Criar(Guid.NewGuid(), Guid.NewGuid(), valor, Agora);

        acao.Should().Throw<DomainException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public void Criar_AcimaDoLimite_Falha()
    {
        var acao = () => Doacao.Criar(Guid.NewGuid(), Guid.NewGuid(), 1_000_000.01m, Agora);

        acao.Should().Throw<DomainException>().WithMessage("*limite permitido*");
    }

    [Fact]
    public void MarcarComoProcessada_LimpaRejeicaoERegistraData()
    {
        var doacao = Doacao.Criar(Guid.NewGuid(), Guid.NewGuid(), 50m, Agora);
        doacao.Rejeitar("erro temporario", Agora);

        doacao.MarcarComoProcessada(Agora.AddMinutes(1));

        doacao.Status.Should().Be(StatusDoacao.Processada);
        doacao.MotivoRejeicao.Should().BeNull();
        doacao.ProcessadaEm.Should().Be(Agora.AddMinutes(1));
    }
}
