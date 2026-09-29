using System.Text.RegularExpressions;
using ConexaoSolidaria.Domain.Exceptions;

namespace ConexaoSolidaria.Domain.ValueObjects;

public readonly partial record struct Cpf
{
    private Cpf(string numero) => Numero = numero;

    public string Numero { get; }

    public string Formatado => $"{Numero[..3]}.{Numero[3..6]}.{Numero[6..9]}-{Numero[9..]}";

    public static Cpf Criar(string? entrada)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(entrada), "CPF e obrigatorio.");

        var digitos = SomenteDigitos().Replace(entrada!, string.Empty);
        DomainException.Require(digitos.Length == 11, "CPF deve conter 11 digitos.");
        DomainException.Require(!digitos.All(d => d == digitos[0]), "CPF invalido.");
        DomainException.Require(DigitoVerificador(digitos, 9) == digitos[9] && DigitoVerificador(digitos, 10) == digitos[10], "CPF invalido.");

        return new Cpf(digitos);
    }

    public static bool TryCriar(string? entrada, out Cpf cpf)
    {
        try
        {
            cpf = Criar(entrada);
            return true;
        }
        catch (DomainException)
        {
            cpf = default;
            return false;
        }
    }

    private static char DigitoVerificador(string digitos, int posicao)
    {
        var peso = posicao + 1;
        var soma = 0;

        for (var i = 0; i < posicao; i++)
        {
            soma += (digitos[i] - '0') * peso--;
        }

        var resto = soma * 10 % 11;
        return (char)('0' + (resto == 10 ? 0 : resto));
    }

    public override string ToString() => Numero;

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex SomenteDigitos();
}
