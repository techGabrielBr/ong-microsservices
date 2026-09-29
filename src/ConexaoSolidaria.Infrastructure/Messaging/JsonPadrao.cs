using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConexaoSolidaria.Infrastructure.Messaging;

public static class JsonPadrao
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
