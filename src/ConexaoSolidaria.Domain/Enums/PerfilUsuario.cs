namespace ConexaoSolidaria.Domain.Enums;

public enum PerfilUsuario
{
    Doador = 1,
    GestorONG = 2
}

public static class Roles
{
    public const string Doador = nameof(PerfilUsuario.Doador);
    public const string GestorONG = nameof(PerfilUsuario.GestorONG);
}
