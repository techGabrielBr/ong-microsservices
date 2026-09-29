using System.Security.Claims;
using ConexaoSolidaria.Api.Dtos;
using ConexaoSolidaria.Api.Services;
using ConexaoSolidaria.Infrastructure.Ledger;

namespace ConexaoSolidaria.Api.Endpoints;

public static class DoacoesEndpoints
{
    public static void MapDoacoesEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/doacoes")
            .WithTags("Doacoes")
            .RequireAuthorization();

        grupo.MapPost("/", DoarAsync)
            .RequireAuthorization(Politicas.SomenteDoador)
            .WithSummary("Envia uma intencao de doacao (processada de forma assincrona)")
            .Produces<DoacaoResponse>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        grupo.MapGet("/minhas", ListarMinhasAsync)
            .WithSummary("Historico de doacoes do usuario autenticado")
            .Produces<IEnumerable<DoacaoResponse>>();

        grupo.MapGet("/campanha/{campanhaId:guid}", ListarPorCampanhaAsync)
            .RequireAuthorization(Politicas.SomenteGestor)
            .WithSummary("Extrato de doacoes de uma campanha (gestao)")
            .Produces<IEnumerable<DoacaoResponse>>();
    }

    private static async Task<IResult> DoarAsync(
        CriarDoacaoRequest request,
        ClaimsPrincipal usuario,
        DoacaoService servico,
        CancellationToken ct)
    {
        var resultado = await servico.RegistrarIntencaoAsync(request.IdCampanha, usuario.ObterId(), request.ValorDoacao, ct);

        return resultado.Sucesso
            ? Results.Accepted($"/api/v1/doacoes/campanha/{request.IdCampanha}", DoacaoResponse.De(resultado.Doacao!))
            : Results.Json(new { erro = resultado.Erro }, statusCode: resultado.StatusCode);
    }

    private static async Task<IResult> ListarMinhasAsync(ClaimsPrincipal usuario, IDoacaoLedger ledger, CancellationToken ct)
    {
        var doacoes = await ledger.ListarPorDoadorAsync(usuario.ObterId(), ct);
        return Results.Ok(doacoes.Select(DoacaoResponse.De));
    }

    private static async Task<IResult> ListarPorCampanhaAsync(Guid campanhaId, IDoacaoLedger ledger, CancellationToken ct)
    {
        var doacoes = await ledger.ListarPorCampanhaAsync(campanhaId, ct);
        return Results.Ok(doacoes.Select(DoacaoResponse.De));
    }
}
