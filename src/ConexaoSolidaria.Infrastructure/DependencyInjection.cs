using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using ConexaoSolidaria.Infrastructure.Ledger;
using ConexaoSolidaria.Infrastructure.Messaging;
using ConexaoSolidaria.Infrastructure.Options;
using ConexaoSolidaria.Infrastructure.Persistence;
using ConexaoSolidaria.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.Configure<AwsOptions>(configuration.GetSection(AwsOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddDbContext<ConexaoSolidariaDbContext>(opcoes =>
            opcoes.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

        services.AddScoped<ICampanhaRepository, CampanhaRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAwsClients();
        services.AddSingleton<IDoacaoLedger, DynamoDbDoacaoLedger>();
        services.AddSingleton<DynamoDbInitializer>();

        services.AddMensageria(configuration);

        return services;
    }

    private static void AddAwsClients(this IServiceCollection services)
    {
        services.AddSingleton<IAmazonDynamoDB>(sp =>
            CriarCliente<IAmazonDynamoDB, AmazonDynamoDBConfig>(
                sp,
                (cred, cfg) => new AmazonDynamoDBClient(cred, cfg),
                cfg => new AmazonDynamoDBClient(cfg)));

        services.AddSingleton<IAmazonSQS>(sp =>
            CriarCliente<IAmazonSQS, AmazonSQSConfig>(
                sp,
                (cred, cfg) => new AmazonSQSClient(cred, cfg),
                cfg => new AmazonSQSClient(cfg)));

        services.AddSingleton<IAmazonSimpleNotificationService>(sp =>
            CriarCliente<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceConfig>(
                sp,
                (cred, cfg) => new AmazonSimpleNotificationServiceClient(cred, cfg),
                cfg => new AmazonSimpleNotificationServiceClient(cfg)));
    }

    private static TCliente CriarCliente<TCliente, TConfig>(
        IServiceProvider sp,
        Func<AWSCredentials, TConfig, TCliente> comCredenciais,
        Func<TConfig, TCliente> cadeiaPadrao)
        where TConfig : ClientConfig, new()
    {
        var aws = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
        var config = new TConfig { RegionEndpoint = RegionEndpoint.GetBySystemName(aws.Region) };

        if (!aws.UsaLocalStack)
        {
            return cadeiaPadrao(config);
        }

        config.ServiceURL = aws.ServiceUrl;
        config.AuthenticationRegion = aws.Region;

        return comCredenciais(new BasicAWSCredentials(aws.AccessKey, aws.SecretKey), config);
    }

    private static void AddMensageria(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration
            .GetSection(MessagingOptions.SectionName)
            .GetValue(nameof(MessagingOptions.Provider), MessagingProvider.RabbitMq);

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<AwsMessagingTopology>();

        if (provider == MessagingProvider.Aws)
        {
            services.AddSingleton<IEventPublisher, AwsEventPublisher>();
            services.AddSingleton<IEventConsumer, AwsEventConsumer>();
            services.AddSingleton<IMessagingTopologyInitializer>(sp => sp.GetRequiredService<AwsMessagingTopology>());
        }
        else
        {
            services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
            services.AddSingleton<IEventConsumer, RabbitMqEventConsumer>();
            services.AddSingleton<IMessagingTopologyInitializer, RabbitMqTopologyInitializer>();
        }
    }
}
