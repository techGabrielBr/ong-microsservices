namespace ConexaoSolidaria.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "conexao-solidaria";
    public string Audience { get; set; } = "conexao-solidaria-clients";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiracaoMinutos { get; set; } = 120;
}
