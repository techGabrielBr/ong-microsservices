using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public sealed class RabbitMqConnectionProvider(IOptions<MessagingOptions> options) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value.RabbitMq;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> ObterConexaoAsync(CancellationToken ct = default)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Usuario,
                Password = _options.Senha,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                ClientProvidedName = "conexao-solidaria"
            };

            _connection = await factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
