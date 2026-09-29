using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;

namespace ConexaoSolidaria.Api.Dtos;

public sealed record RegistrarDoadorRequest(string NomeCompleto, string Email, string Cpf, string Senha);

public sealed record LoginRequest(string Email, string Senha);

public sealed record LoginResponse(string AccessToken, DateTime ExpiraEm, string Perfil, string TokenType = "Bearer");

public sealed record UsuarioResponse(Guid Id, string NomeCompleto, string Email, string Cpf, string Perfil);

public sealed record CriarCampanhaRequest(
    string Titulo,
    string Descricao,
    DateTime DataInicio,
    DateTime DataFim,
    decimal MetaFinanceira);

public sealed record AtualizarCampanhaRequest(
    string Titulo,
    string Descricao,
    DateTime DataInicio,
    DateTime DataFim,
    decimal MetaFinanceira,
    StatusCampanha Status);

public sealed record CampanhaResponse(
    Guid Id,
    string Titulo,
    string Descricao,
    DateTime DataInicio,
    DateTime DataFim,
    decimal MetaFinanceira,
    decimal ValorArrecadado,
    decimal PercentualAtingido,
    string Status,
    DateTime CriadaEm,
    DateTime AtualizadaEm)
{
    public static CampanhaResponse De(Campanha c) => new(
        c.Id,
        c.Titulo,
        c.Descricao,
        c.DataInicio,
        c.DataFim,
        c.MetaFinanceira,
        c.ValorArrecadado,
        c.PercentualAtingido,
        c.Status.ToString(),
        c.CriadaEm,
        c.AtualizadaEm);
}

/// Painel de transparencia: exposto sem autenticacao.
public sealed record CampanhaPublicaResponse(
    Guid Id,
    string Titulo,
    decimal MetaFinanceira,
    decimal ValorTotalArrecadado,
    decimal PercentualAtingido,
    DateTime DataFim)
{
    public static CampanhaPublicaResponse De(Campanha c) => new(
        c.Id,
        c.Titulo,
        c.MetaFinanceira,
        c.ValorArrecadado,
        c.PercentualAtingido,
        c.DataFim);
}

public sealed record CriarDoacaoRequest(Guid IdCampanha, decimal ValorDoacao);

public sealed record DoacaoResponse(
    Guid Id,
    Guid CampanhaId,
    Guid DoadorId,
    decimal Valor,
    string Status,
    DateTime CriadaEm,
    DateTime? ProcessadaEm,
    string? MotivoRejeicao)
{
    public static DoacaoResponse De(Doacao d) => new(
        d.Id,
        d.CampanhaId,
        d.DoadorId,
        d.Valor,
        d.Status.ToString(),
        d.CriadaEm,
        d.ProcessadaEm,
        d.MotivoRejeicao);
}
