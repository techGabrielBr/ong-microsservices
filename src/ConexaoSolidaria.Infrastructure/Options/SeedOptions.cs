namespace ConexaoSolidaria.Infrastructure.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Habilitado { get; set; } = true;
    public string GestorNome { get; set; } = "Gestor Padrao ONG";
    public string GestorEmail { get; set; } = "gestor@conexaosolidaria.org";
    public string GestorCpf { get; set; } = "39053344705";
    public string GestorSenha { get; set; } = "Gestor@123";
}
