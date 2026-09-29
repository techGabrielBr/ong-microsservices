namespace ConexaoSolidaria.Contracts.Events;

public sealed record DoacaoRecebidaEvent
{
    public const string Nome = "DoacaoRecebidaEvent";

    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid DoacaoId { get; init; }
    public Guid CampanhaId { get; init; }
    public Guid DoadorId { get; init; }
    public decimal Valor { get; init; }
    public DateTime OcorridoEm { get; init; }
}
