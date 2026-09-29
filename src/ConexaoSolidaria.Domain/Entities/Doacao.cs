using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.Exceptions;

namespace ConexaoSolidaria.Domain.Entities;

public sealed class Doacao
{
    private Doacao()
    {
    }

    public Guid Id { get; private set; }
    public Guid CampanhaId { get; private set; }
    public Guid DoadorId { get; private set; }
    public decimal Valor { get; private set; }
    public StatusDoacao Status { get; private set; }
    public string? MotivoRejeicao { get; private set; }
    public DateTime CriadaEm { get; private set; }
    public DateTime? ProcessadaEm { get; private set; }

    public static Doacao Criar(Guid campanhaId, Guid doadorId, decimal valor, DateTime agoraUtc)
    {
        DomainException.Require(campanhaId != Guid.Empty, "Campanha e obrigatoria.");
        DomainException.Require(doadorId != Guid.Empty, "Doador e obrigatorio.");
        DomainException.Require(valor > 0, "Valor da doacao deve ser maior que zero.");
        DomainException.Require(valor <= 1_000_000m, "Valor da doacao excede o limite permitido.");

        return new Doacao
        {
            Id = Guid.NewGuid(),
            CampanhaId = campanhaId,
            DoadorId = doadorId,
            Valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero),
            Status = StatusDoacao.Pendente,
            CriadaEm = agoraUtc
        };
    }

    public static Doacao Restaurar(
        Guid id,
        Guid campanhaId,
        Guid doadorId,
        decimal valor,
        StatusDoacao status,
        string? motivoRejeicao,
        DateTime criadaEm,
        DateTime? processadaEm) => new()
        {
            Id = id,
            CampanhaId = campanhaId,
            DoadorId = doadorId,
            Valor = valor,
            Status = status,
            MotivoRejeicao = motivoRejeicao,
            CriadaEm = criadaEm,
            ProcessadaEm = processadaEm
        };

    public void MarcarComoProcessada(DateTime agoraUtc)
    {
        Status = StatusDoacao.Processada;
        MotivoRejeicao = null;
        ProcessadaEm = agoraUtc;
    }

    public void Rejeitar(string motivo, DateTime agoraUtc)
    {
        Status = StatusDoacao.Rejeitada;
        MotivoRejeicao = motivo;
        ProcessadaEm = agoraUtc;
    }
}
