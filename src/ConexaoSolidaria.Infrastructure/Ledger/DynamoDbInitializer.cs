using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure.Ledger;

public sealed class DynamoDbInitializer(
    IAmazonDynamoDB dynamo,
    IOptions<AwsOptions> options,
    ILogger<DynamoDbInitializer> logger)
{
    private readonly AwsOptions _options = options.Value;

    public async Task GarantirTabelaAsync(CancellationToken ct = default)
    {
        var tabela = _options.TabelaDoacoes;

        try
        {
            await dynamo.DescribeTableAsync(tabela, ct);
            logger.LogInformation("Tabela DynamoDB {Tabela} ja existe.", tabela);
            return;
        }
        catch (ResourceNotFoundException)
        {
            logger.LogInformation("Criando tabela DynamoDB {Tabela}.", tabela);
        }

        await dynamo.CreateTableAsync(
            new CreateTableRequest
            {
                TableName = tabela,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition("CampanhaId", ScalarAttributeType.S),
                    new AttributeDefinition("DoacaoId", ScalarAttributeType.S),
                    new AttributeDefinition("DoadorId", ScalarAttributeType.S),
                    new AttributeDefinition("CriadaEm", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("CampanhaId", KeyType.HASH),
                    new KeySchemaElement("DoacaoId", KeyType.RANGE)
                ],
                GlobalSecondaryIndexes =
                [
                    new GlobalSecondaryIndex
                    {
                        IndexName = DynamoDbDoacaoLedger.IndiceDoador,
                        KeySchema =
                        [
                            new KeySchemaElement("DoadorId", KeyType.HASH),
                            new KeySchemaElement("CriadaEm", KeyType.RANGE)
                        ],
                        Projection = new Projection { ProjectionType = ProjectionType.ALL }
                    }
                ]
            },
            ct);

        await EsperarTabelaAtivaAsync(tabela, ct);
    }

    private async Task EsperarTabelaAtivaAsync(string tabela, CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            var descricao = await dynamo.DescribeTableAsync(tabela, ct);
            if (descricao.Table.TableStatus == TableStatus.ACTIVE)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        throw new TimeoutException($"Tabela {tabela} nao ficou ativa a tempo.");
    }
}
