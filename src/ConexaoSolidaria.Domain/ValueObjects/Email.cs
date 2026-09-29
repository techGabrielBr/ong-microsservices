using System.Text.RegularExpressions;
using ConexaoSolidaria.Domain.Exceptions;

namespace ConexaoSolidaria.Domain.ValueObjects;

public readonly partial record struct Email
{
    private Email(string endereco) => Endereco = endereco;

    public string Endereco { get; }

    public static Email Criar(string? entrada)
    {
        DomainException.Require(!string.IsNullOrWhiteSpace(entrada), "E-mail e obrigatorio.");

        var normalizado = entrada!.Trim().ToLowerInvariant();
        DomainException.Require(Formato().IsMatch(normalizado), "E-mail invalido.");

        return new Email(normalizado);
    }

    public override string ToString() => Endereco;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[a-z]{2,}$")]
    private static partial Regex Formato();
}
