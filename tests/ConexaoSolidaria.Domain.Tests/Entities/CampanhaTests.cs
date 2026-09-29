using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;
using FluentAssertions;

namespace ConexaoSolidaria.Domain.Tests.Entities;

public class CampanhaTests
{
    private static readonly DateTime Agora = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Gestor = Guid.NewGuid();

    private static Campanha CriarValida(DateTime? dataFim = null, decimal meta = 10_000m) => Campanha.Criar(
        "Campanha do Agasalho",
        "Arrecadacao de agasalhos para o inverno.",
        Agora,
        dataFim ?? Agora.AddDays(30),
        meta,
        Gestor,
        Agora);

    [Fact]
    public void Criar_ComDadosValidos_NasceAtivaESemArrecadacao()
    {
        var campanha = CriarValida();

        campanha.Status.Should().Be(StatusCampanha.Ativa);
        campanha.ValorArrecadado.Should().Be(0m);
        campanha.CriadaPor.Should().Be(Gestor);
        campanha.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Criar_ComDataFimNoPassado_Falha()
    {
        var acao = () => Campanha.Criar(
            "Campanha encerrada",
            "Periodo ja vencido.",
            Agora.AddDays(-30),
            Agora.AddDays(-1),
            10_000m,
            Gestor,
            Agora);

        acao.Should().Throw<DomainException>().WithMessage("*termino nao pode estar no passado*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1500.55)]
    public void Criar_ComMetaNaoPositiva_Falha(decimal meta)
    {
        var acao = () => CriarValida(meta: meta);

        acao.Should().Throw<DomainException>().WithMessage("*Meta financeira deve ser maior que zero*");
    }

    [Fact]
    public void Criar_SemTitulo_Falha()
    {
        var acao = () => Campanha.Criar("  ", "descricao", Agora, Agora.AddDays(5), 100m, Gestor, Agora);

        acao.Should().Throw<DomainException>().WithMessage("*Titulo e obrigatorio*");
    }

    [Fact]
    public void PodeReceberDoacao_CampanhaAtivaEVigente_RetornaVerdadeiro()
    {
        CriarValida().PodeReceberDoacao(Agora).Should().BeTrue();
    }

    [Fact]
    public void PodeReceberDoacao_AposDataFim_RetornaFalso()
    {
        var campanha = CriarValida(dataFim: Agora.AddDays(1));

        campanha.PodeReceberDoacao(Agora.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void GarantirQuePodeReceberDoacao_CampanhaCancelada_Falha()
    {
        var campanha = CriarValida();
        campanha.Cancelar(Agora);

        var acao = () => campanha.GarantirQuePodeReceberDoacao(Agora);

        acao.Should().Throw<DomainException>().WithMessage("*cancelada nao aceita doacoes*");
    }

    [Fact]
    public void GarantirQuePodeReceberDoacao_CampanhaConcluida_Falha()
    {
        var campanha = CriarValida();
        campanha.Concluir(Agora);

        var acao = () => campanha.GarantirQuePodeReceberDoacao(Agora);

        acao.Should().Throw<DomainException>().WithMessage("*concluida nao aceita doacoes*");
    }

    [Fact]
    public void RegistrarArrecadacao_SomaValoresEAtualizaPercentual()
    {
        var campanha = CriarValida(meta: 1_000m);

        campanha.RegistrarArrecadacao(250m, Agora);
        campanha.RegistrarArrecadacao(100m, Agora);

        campanha.ValorArrecadado.Should().Be(350m);
        campanha.PercentualAtingido.Should().Be(35m);
    }

    [Fact]
    public void RegistrarArrecadacao_ComValorNaoPositivo_Falha()
    {
        var campanha = CriarValida();

        var acao = () => campanha.RegistrarArrecadacao(0m, Agora);

        acao.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancelar_CampanhaJaCancelada_Falha()
    {
        var campanha = CriarValida();
        campanha.Cancelar(Agora);

        var acao = () => campanha.Cancelar(Agora);

        acao.Should().Throw<DomainException>().WithMessage("*Somente campanhas ativas*");
    }

    [Fact]
    public void Atualizar_CampanhaCancelada_Falha()
    {
        var campanha = CriarValida();
        campanha.Cancelar(Agora);

        var acao = () => campanha.Atualizar("Novo titulo", "Nova descricao", Agora, Agora.AddDays(10), 500m, StatusCampanha.Ativa, Agora);

        acao.Should().Throw<DomainException>().WithMessage("*cancelada nao pode ser editada*");
    }

    [Fact]
    public void Atualizar_ConcluindoCampanhaComDataFimPassada_EPermitido()
    {
        var campanha = CriarValida();

        campanha.Atualizar(
            "Campanha do Agasalho",
            "Encerrada",
            Agora.AddDays(-30),
            Agora.AddDays(-1),
            10_000m,
            StatusCampanha.Concluida,
            Agora);

        campanha.Status.Should().Be(StatusCampanha.Concluida);
    }
}
