using System.Globalization;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ConexaoSolidaria.Infrastructure.Ledger;

public sealed class DynamoDbDoacaoLedger(IAmazonDynamoDB dynamo, IOptions<AwsOptions> options) : IDoacaoLedger
{
    private readonly AwsOptions _options = options.Value;

    public const string IndiceDoador = "doador-index";

    public async Task SalvarAsync(Doacao doacao, CancellationToken ct = default)
    {
        await dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _options.TabelaDoacoes,
                Item = ParaItem(doacao)
            },
            ct);
    }

    public async Task<Doacao?> ObterAsync(Guid campanhaId, Guid doacaoId, CancellationToken ct = default)
    {
        var resposta = await dynamo.GetItemAsync(
            new GetItemRequest
            {
                TableName = _options.TabelaDoacoes,
                Key = Chave(campanhaId, doacaoId),
                ConsistentRead = true
            },
            ct);

        return resposta.IsItemSet ? ParaDoacao(resposta.Item) : null;
    }

    public async Task<IReadOnlyList<Doacao>> ListarPorCampanhaAsync(Guid campanhaId, CancellationToken ct = default)
    {
        var resposta = await dynamo.QueryAsync(
            new QueryRequest
            {
                TableName = _options.TabelaDoacoes,
                KeyConditionExpression = "CampanhaId = :campanha",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":campanha"] = new() { S = campanhaId.ToString() }
                }
            },
            ct);

        return [.. resposta.Items.Select(ParaDoacao)];
    }

    public async Task<IReadOnlyList<Doacao>> ListarPorDoadorAsync(Guid doadorId, CancellationToken ct = default)
    {
        var resposta = await dynamo.QueryAsync(
            new QueryRequest
            {
                TableName = _options.TabelaDoacoes,
                IndexName = IndiceDoador,
                KeyConditionExpression = "DoadorId = :doador",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":doador"] = new() { S = doadorId.ToString() }
                },
                ScanIndexForward = false
            },
            ct);

        return [.. resposta.Items.Select(ParaDoacao)];
    }

    public async Task<bool> TentarMarcarComoProcessadaAsync(Guid campanhaId, Guid doacaoId, DateTime agoraUtc, CancellationToken ct = default)
    {
        try
        {
            await dynamo.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName = _options.TabelaDoacoes,
                    Key = Chave(campanhaId, doacaoId),
                    UpdateExpression = "SET #status = :processada, ProcessadaEm = :agora REMOVE MotivoRejeicao",
                    ConditionExpression = "attribute_exists(DoacaoId) AND #status = :pendente",
                    ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":processada"] = new() { N = ((int)StatusDoacao.Processada).ToString() },
                        [":pendente"] = new() { N = ((int)StatusDoacao.Pendente).ToString() },
                        [":agora"] = new() { S = agoraUtc.ToString("O", CultureInfo.InvariantCulture) }
                    }
                },
                ct);

            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task MarcarComoRejeitadaAsync(Guid campanhaId, Guid doacaoId, string motivo, DateTime agoraUtc, CancellationToken ct = default)
    {
        await dynamo.UpdateItemAsync(
            new UpdateItemRequest
            {
                TableName = _options.TabelaDoacoes,
                Key = Chave(campanhaId, doacaoId),
                UpdateExpression = "SET #status = :rejeitada, MotivoRejeicao = :motivo, ProcessadaEm = :agora",
                ConditionExpression = "attribute_exists(DoacaoId)",
                ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":rejeitada"] = new() { N = ((int)StatusDoacao.Rejeitada).ToString() },
                    [":motivo"] = new() { S = motivo },
                    [":agora"] = new() { S = agoraUtc.ToString("O", CultureInfo.InvariantCulture) }
                }
            },
            ct);
    }

    private static Dictionary<string, AttributeValue> Chave(Guid campanhaId, Guid doacaoId) => new()
    {
        ["CampanhaId"] = new AttributeValue { S = campanhaId.ToString() },
        ["DoacaoId"] = new AttributeValue { S = doacaoId.ToString() }
    };

    private static Dictionary<string, AttributeValue> ParaItem(Doacao doacao)
    {
        var item = Chave(doacao.CampanhaId, doacao.Id);
        item["DoadorId"] = new AttributeValue { S = doacao.DoadorId.ToString() };
        item["Valor"] = new AttributeValue { N = doacao.Valor.ToString(CultureInfo.InvariantCulture) };
        item["Status"] = new AttributeValue { N = ((int)doacao.Status).ToString() };
        item["CriadaEm"] = new AttributeValue { S = doacao.CriadaEm.ToString("O", CultureInfo.InvariantCulture) };

        if (doacao.ProcessadaEm is not null)
        {
            item["ProcessadaEm"] = new AttributeValue { S = doacao.ProcessadaEm.Value.ToString("O", CultureInfo.InvariantCulture) };
        }

        if (!string.IsNullOrWhiteSpace(doacao.MotivoRejeicao))
        {
            item["MotivoRejeicao"] = new AttributeValue { S = doacao.MotivoRejeicao };
        }

        return item;
    }

    private static Doacao ParaDoacao(Dictionary<string, AttributeValue> item) => Doacao.Restaurar(
        Guid.Parse(item["DoacaoId"].S),
        Guid.Parse(item["CampanhaId"].S),
        Guid.Parse(item["DoadorId"].S),
        decimal.Parse(item["Valor"].N, CultureInfo.InvariantCulture),
        (StatusDoacao)int.Parse(item["Status"].N),
        item.TryGetValue("MotivoRejeicao", out var motivo) ? motivo.S : null,
        DateTime.Parse(item["CriadaEm"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        item.TryGetValue("ProcessadaEm", out var processada) && processada.S is not null
            ? DateTime.Parse(processada.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null);
}
