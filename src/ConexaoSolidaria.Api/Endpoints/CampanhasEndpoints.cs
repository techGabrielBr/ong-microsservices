using System.Security.Claims;
using ConexaoSolidaria.Api.Dtos;
using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Infrastructure.Persistence;

namespace ConexaoSolidaria.Api.Endpoints;

public static class CampanhasEndpoints
{
    public static void MapCampanhasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/campanhas")
            .WithTags("Campanhas")
            .RequireAuthorization(Politicas.SomenteGestor);

        grupo.MapGet("/", ListarAsync)
            .WithSummary("Lista todas as campanhas (gestao)")
            .Produces<IEnumerable<CampanhaResponse>>();

        grupo.MapGet("/{id:guid}", ObterAsync)
            .WithSummary("Detalha uma campanha")
            .Produces<CampanhaResponse>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/", CriarAsync)
            .WithSummary("Cria uma campanha")
            .Produces<CampanhaResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        grupo.MapPut("/{id:guid}", AtualizarAsync)
            .WithSummary("Edita uma campanha")
            .Produces<CampanhaResponse>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/cancelar", CancelarAsync)
            .WithSummary("Cancela uma campanha")
            .Produces<CampanhaResponse>();

        grupo.MapPost("/{id:guid}/concluir", ConcluirAsync)
            .WithSummary("Conclui uma campanha")
            .Produces<CampanhaResponse>();
    }

    private static async Task<IResult> ListarAsync(ICampanhaRepository repositorio, CancellationToken ct)
    {
        var campanhas = await repositorio.ListarTodasAsync(ct);
        return Results.Ok(campanhas.Select(CampanhaResponse.De));
    }

    private static async Task<IResult> ObterAsync(Guid id, ICampanhaRepository repositorio, CancellationToken ct)
    {
        var campanha = await repositorio.ObterPorIdAsync(id, ct);
        return campanha is null ? Results.NotFound() : Results.Ok(CampanhaResponse.De(campanha));
    }

    private static async Task<IResult> CriarAsync(
        CriarCampanhaRequest request,
        ClaimsPrincipal usuario,
        ICampanhaRepository repositorio,
        CancellationToken ct)
    {
        var campanha = Campanha.Criar(
            request.Titulo,
            request.Descricao,
            request.DataInicio.ToUniversalTime(),
            request.DataFim.ToUniversalTime(),
            request.MetaFinanceira,
            usuario.ObterId(),
            DateTime.UtcNow);

        await repositorio.AdicionarAsync(campanha, ct);
        await repositorio.SalvarAsync(ct);

        return Results.Created($"/api/v1/campanhas/{campanha.Id}", CampanhaResponse.De(campanha));
    }

    private static async Task<IResult> AtualizarAsync(
        Guid id,
        AtualizarCampanhaRequest request,
        ICampanhaRepository repositorio,
        CancellationToken ct)
    {
        var campanha = await repositorio.ObterPorIdAsync(id, ct);
        if (campanha is null)
        {
            return Results.NotFound();
        }

        campanha.Atualizar(
            request.Titulo,
            request.Descricao,
            request.DataInicio.ToUniversalTime(),
            request.DataFim.ToUniversalTime(),
            request.MetaFinanceira,
            request.Status,
            DateTime.UtcNow);

        await repositorio.SalvarAsync(ct);
        return Results.Ok(CampanhaResponse.De(campanha));
    }

    private static Task<IResult> CancelarAsync(Guid id, ICampanhaRepository repositorio, CancellationToken ct) =>
        TransicionarAsync(id, repositorio, c => c.Cancelar(DateTime.UtcNow), ct);

    private static Task<IResult> ConcluirAsync(Guid id, ICampanhaRepository repositorio, CancellationToken ct) =>
        TransicionarAsync(id, repositorio, c => c.Concluir(DateTime.UtcNow), ct);

    private static async Task<IResult> TransicionarAsync(
        Guid id,
        ICampanhaRepository repositorio,
        Action<Campanha> transicao,
        CancellationToken ct)
    {
        var campanha = await repositorio.ObterPorIdAsync(id, ct);
        if (campanha is null)
        {
            return Results.NotFound();
        }

        transicao(campanha);
        await repositorio.SalvarAsync(ct);

        return Results.Ok(CampanhaResponse.De(campanha));
    }
}

public static class Politicas
{
    public const string SomenteGestor = nameof(SomenteGestor);
    public const string SomenteDoador = nameof(SomenteDoador);
}

public static class ClaimsPrincipalExtensions
{
    public static Guid ObterId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        return Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }

    public static bool EhGestor(this ClaimsPrincipal principal) => principal.IsInRole(Roles.GestorONG);
}
