namespace ConexaoSolidaria.Infrastructure.Options;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";

    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = "test";
    public string SecretKey { get; set; } = "test";

    /// LocalStack: http://localhost:4566. Vazio = AWS real com credenciais da cadeia padrao.
    public string? ServiceUrl { get; set; } = "http://localhost:4566";

    public string TabelaDoacoes { get; set; } = "doacoes";

    public bool UsaLocalStack => !string.IsNullOrWhiteSpace(ServiceUrl);
}
