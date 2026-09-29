using ConexaoSolidaria.Api.Dtos;
using ConexaoSolidaria.Infrastructure.Persistence;

namespace ConexaoSolidaria.Api.Endpoints;

public static class PainelPublicoEndpoints
{
    public static void MapPainelPublicoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/publico").WithTags("Painel de Transparencia");

        grupo.MapGet("/campanhas", ListarAtivasAsync)
            .AllowAnonymous()
            .WithSummary("Campanhas ativas com o total ja arrecadado")
            .Produces<IEnumerable<CampanhaPublicaResponse>>();

        grupo.MapGet("/campanhas/{id:guid}", ObterAtivaAsync)
            .AllowAnonymous()
            .WithSummary("Detalhe publico de uma campanha ativa")
            .Produces<CampanhaPublicaResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListarAtivasAsync(ICampanhaRepository repositorio, CancellationToken ct)
    {
        var campanhas = await repositorio.ListarAtivasAsync(ct);
        return Results.Ok(campanhas.Select(CampanhaPublicaResponse.De));
    }

    private static async Task<IResult> ObterAtivaAsync(Guid id, ICampanhaRepository repositorio, CancellationToken ct)
    {
        var campanha = await repositorio.ObterPorIdAsync(id, ct);

        return campanha is null || campanha.Status != Domain.Enums.StatusCampanha.Ativa
            ? Results.NotFound()
            : Results.Ok(CampanhaPublicaResponse.De(campanha));
    }
}
