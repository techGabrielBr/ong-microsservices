using ConexaoSolidaria.Api.Dtos;
using ConexaoSolidaria.Domain.Entities;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Domain.ValueObjects;
using ConexaoSolidaria.Infrastructure.Persistence;
using ConexaoSolidaria.Infrastructure.Security;

namespace ConexaoSolidaria.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/v1/auth").WithTags("Autenticacao");

        grupo.MapPost("/doadores", RegistrarDoadorAsync)
            .AllowAnonymous()
            .WithSummary("Cadastro publico de doador")
            .Produces<UsuarioResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        grupo.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithSummary("Autentica e devolve o token JWT")
            .Produces<LoginResponse>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> RegistrarDoadorAsync(
        RegistrarDoadorRequest request,
        IUsuarioRepository repositorio,
        IPasswordHasher hasher,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length < 8)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["senha"] = ["A senha deve ter ao menos 8 caracteres."]
            });
        }

        var email = Email.Criar(request.Email);
        var cpf = Cpf.Criar(request.Cpf);

        if (await repositorio.ExisteEmailAsync(email.Endereco, ct))
        {
            return Results.Conflict(new { erro = "E-mail ja cadastrado." });
        }

        if (await repositorio.ExisteCpfAsync(cpf.Numero, ct))
        {
            return Results.Conflict(new { erro = "CPF ja cadastrado." });
        }

        var doador = Usuario.Criar(
            request.NomeCompleto,
            email.Endereco,
            cpf.Numero,
            hasher.Hash(request.Senha),
            PerfilUsuario.Doador,
            DateTime.UtcNow);

        await repositorio.AdicionarAsync(doador, ct);
        await repositorio.SalvarAsync(ct);

        var resposta = new UsuarioResponse(doador.Id, doador.NomeCompleto, doador.Email, cpf.Formatado, doador.Perfil.ToString());
        return Results.Created($"/api/v1/auth/doadores/{doador.Id}", resposta);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUsuarioRepository repositorio,
        IPasswordHasher hasher,
        ITokenService tokens,
        CancellationToken ct)
    {
        var usuario = await repositorio.ObterPorEmailAsync(request.Email ?? string.Empty, ct);

        if (usuario is null || !usuario.Ativo || !hasher.Verificar(request.Senha ?? string.Empty, usuario.SenhaHash))
        {
            return Results.Json(new { erro = "Credenciais invalidas." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var token = tokens.Gerar(usuario);
        return Results.Ok(new LoginResponse(token.AccessToken, token.ExpiraEm, token.Perfil));
    }
}
