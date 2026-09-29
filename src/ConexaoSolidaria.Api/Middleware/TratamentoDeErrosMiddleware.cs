using System.Text.Json;
using ConexaoSolidaria.Domain.Exceptions;

namespace ConexaoSolidaria.Api.Middleware;

public sealed class TratamentoDeErrosMiddleware(RequestDelegate next, ILogger<TratamentoDeErrosMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            logger.LogWarning("Regra de negocio violada: {Mensagem}", ex.Message);
            await EscreverAsync(context, StatusCodes.Status422UnprocessableEntity, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro nao tratado ao processar {Metodo} {Rota}.", context.Request.Method, context.Request.Path);
            await EscreverAsync(context, StatusCodes.Status500InternalServerError, "Erro interno ao processar a requisicao.");
        }
    }

    private static async Task EscreverAsync(HttpContext context, int status, string mensagem)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new { erro = mensagem, status }));
    }
}
