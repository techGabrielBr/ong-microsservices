using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Infrastructure.Ledger;
using ConexaoSolidaria.Infrastructure.Messaging;
using ConexaoSolidaria.Infrastructure.Options;
using ConexaoSolidaria.Infrastructure.Persistence;
using ConexaoSolidaria.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure;

public static class InfraestruturaInicializador
{
    public static async Task PrepararAsync(IServiceProvider services, bool aplicarMigrations, CancellationToken ct = default)
    {
        using var escopo = services.CreateScope();
        var sp = escopo.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InfraestruturaInicializador));

        if (aplicarMigrations)
        {
            var db = sp.GetRequiredService<ConexaoSolidariaDbContext>();
            await ExecutarComRetentativaAsync(logger, "migrations Postgres", () => db.Database.MigrateAsync(ct));
        }

        await ExecutarComRetentativaAsync(
            logger,
            "tabela DynamoDB",
            () => sp.GetRequiredService<DynamoDbInitializer>().GarantirTabelaAsync(ct));

        await ExecutarComRetentativaAsync(
            logger,
            "topologia de mensageria",
            () => sp.GetRequiredService<IMessagingTopologyInitializer>().GarantirTopologiaAsync(ct));

        if (aplicarMigrations)
        {
            await SemearGestorAsync(sp, logger, ct);
        }
    }

    private static async Task SemearGestorAsync(IServiceProvider sp, ILogger logger, CancellationToken ct)
    {
        var seed = sp.GetService<IOptions<SeedOptions>>()?.Value ?? new SeedOptions();
        if (!seed.Habilitado)
        {
            return;
        }

        var repositorio = sp.GetRequiredService<IUsuarioRepository>();
        if (await repositorio.ExisteEmailAsync(seed.GestorEmail, ct))
        {
            return;
        }

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var gestor = Usuario.Criar(
            seed.GestorNome,
            seed.GestorEmail,
            seed.GestorCpf,
            hasher.Hash(seed.GestorSenha),
            PerfilUsuario.GestorONG,
            DateTime.UtcNow);

        await repositorio.AdicionarAsync(gestor, ct);
        await repositorio.SalvarAsync(ct);

        logger.LogInformation("Gestor inicial criado: {Email}", seed.GestorEmail);
    }

    private static async Task ExecutarComRetentativaAsync(ILogger logger, string etapa, Func<Task> acao)
    {
        const int maxTentativas = 10;

        for (var tentativa = 1; tentativa <= maxTentativas; tentativa++)
        {
            try
            {
                await acao();
                logger.LogInformation("Etapa concluida: {Etapa}.", etapa);
                return;
            }
            catch (Exception ex) when (tentativa < maxTentativas)
            {
                logger.LogWarning(ex, "Falha na etapa {Etapa} (tentativa {Tentativa}/{Max}). Nova tentativa em 5s.", etapa, tentativa, maxTentativas);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
