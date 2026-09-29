using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;

namespace ConexaoSolidaria.Domain.Entities;

public sealed class Campanha
{
    private Campanha()
    {
        Titulo = string.Empty;
        Descricao = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Titulo { get; private set; }
    public string Descricao { get; private set; }
    public DateTime DataInicio { get; private set; }
    public DateTime DataFim { get; private set; }
    public decimal MetaFinanceira { get; private set; }
    public StatusCampanha Status { get; private set; }
    public decimal ValorArrecadado { get; private set; }
    public Guid CriadaPor { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime AtualizadaEm { get; private set; }

    public decimal PercentualAtingido => MetaFinanceira == 0 ? 0 : Math.Round(ValorArrecadado / MetaFinanceira * 100, 2);

    public static Campanha Criar(
        string titulo,
        string descricao,
        DateTime dataInicio,
        DateTime dataFim,
        decimal metaFinanceira,
        Guid criadaPor,
        DateTime agoraUtc)
    {
        Validar(titulo, descricao, dataInicio, dataFim, metaFinanceira, agoraUtc);

        return new Campanha
        {
            Id = Guid.NewGuid(),
            Titulo = titulo.Trim(),
            Descricao = descricao.Trim(),
            DataInicio = DateTime.SpecifyKind(dataInicio, DateTimeKind.Utc),
            DataFim = DateTime.SpecifyKind(dataFim, DateTimeKind.Utc),
            MetaFinanceira = metaFinanceira,
            Status = StatusCampanha.Ativa,
            ValorArrecadado = 0m,
            CriadaPor = criadaPor,
            CriadaEm = agoraUtc,
            AtualizadaEm = agoraUtc
        };
    }

    public void Atualizar(
        string titulo,
        string descricao,
        DateTime dataInicio,
        DateTime dataFim,
        decimal metaFinanceira,
        StatusCampanha status,
        DateTime agoraUtc)
    {
        DomainException.Require(Status != StatusCampanha.Cancelada, "Campanha cancelada nao pode ser editada.");
        Validar(titulo, descricao, dataInicio, dataFim, metaFinanceira, agoraUtc, validarDataFimFutura: status == StatusCampanha.Ativa);

        Titulo = titulo.Trim();
        Descricao = descricao.Trim();
        DataInicio = DateTime.SpecifyKind(dataInicio, DateTimeKind.Utc);
        DataFim = DateTime.SpecifyKind(dataFim, DateTimeKind.Utc);
        MetaFinanceira = metaFinanceira;
        Status = status;
        AtualizadaEm = agoraUtc;
    }

    public bool PodeReceberDoacao(DateTime agoraUtc) => Status == StatusCampanha.Ativa && DataFim >= agoraUtc;

    public void GarantirQuePodeReceberDoacao(DateTime agoraUtc)
    {
        DomainException.Require(Status != StatusCampanha.Cancelada, "Campanha cancelada nao aceita doacoes.");
        DomainException.Require(Status != StatusCampanha.Concluida, "Campanha concluida nao aceita doacoes.");
        DomainException.Require(DataFim >= agoraUtc, "Campanha encerrada nao aceita doacoes.");
    }

    public void RegistrarArrecadacao(decimal valor, DateTime agoraUtc)
    {
        DomainException.Require(valor > 0, "Valor arrecadado deve ser maior que zero.");

        ValorArrecadado += valor;
        AtualizadaEm = agoraUtc;
    }

    public void Cancelar(DateTime agoraUtc)
    {
        DomainException.Require(Status == StatusCampanha.Ativa, "Somente campanhas ativas podem ser canceladas.");

        Status = StatusCampanha.Cancelada;
        AtualizadaEm = agoraUtc;
    }

    public void Concluir(DateTime agoraUtc)
    {
        DomainException.Require(Status == StatusCampanha.Ativa, "Somente campanhas ativas podem ser concluidas.");

        Status = StatusCampanha.Concluida;
        AtualizadaEm = agoraUtc;
    }

    private static void Validar(
        string titulo,
        string descricao,
        DateTime dataInicio,
        DateTime dataFim,
        decimal metaFinanceira,
        DateTime agoraUtc,
        bool validarDataFimFutura = true)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(titulo), "Titulo e obrigatorio.");
        DomainException.Require(titulo.Trim().Length <= 150, "Titulo deve ter no maximo 150 caracteres.");
        DomainException.Require(!string.IsNullOrWhiteSpace(descricao), "Descricao e obrigatoria.");
        DomainException.Require(metaFinanceira > 0, "Meta financeira deve ser maior que zero.");
        DomainException.Require(dataFim > dataInicio, "Data de termino deve ser posterior a data de inicio.");

        if (validarDataFimFutura)
        {
            DomainException.Require(dataFim > agoraUtc, "Data de termino nao pode estar no passado.");
        }
    }
}
