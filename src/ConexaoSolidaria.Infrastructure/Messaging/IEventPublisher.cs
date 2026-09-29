namespace ConexaoSolidaria.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublicarAsync<TEvento>(TEvento evento, string nomeEvento, CancellationToken ct = default) where TEvento : class;
}

public interface IEventConsumer
{
    /// O handler deve lancar excecao para sinalizar falha; a mensagem entao vai para a DLQ.
    Task ConsumirAsync(Func<string, CancellationToken, Task> handler, CancellationToken ct);
}

public interface IMessagingTopologyInitializer
{
    Task GarantirTopologiaAsync(CancellationToken ct = default);
}
