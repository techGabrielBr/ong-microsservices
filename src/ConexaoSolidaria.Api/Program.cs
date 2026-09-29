using System.Text;
using System.Text.Json.Serialization;
using ConexaoSolidaria.Api.Endpoints;
using ConexaoSolidaria.Api.Middleware;
using ConexaoSolidaria.Api.Services;
using ConexaoSolidaria.Domain.Enums;
using ConexaoSolidaria.Infrastructure;
using ConexaoSolidaria.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddScoped<DoacaoService>();

// As respostas expõem Status como texto ("Ativa"); sem este conversor a entrada
// só aceitaria o número correspondente, o que quebraria a simetria do contrato.
builder.Services.ConfigureHttpJsonOptions(opcoes =>
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opcoes =>
    {
        opcoes.RequireHttpsMetadata = false;
        opcoes.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(Politicas.SomenteGestor, p => p.RequireRole(Roles.GestorONG))
    .AddPolicy(Politicas.SomenteDoador, p => p.RequireRole(Roles.Doador));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opcoes =>
{
    opcoes.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Conexao Solidaria - API",
        Version = "v1",
        Description = "API de campanhas, doadores e intencoes de doacao da ONG Esperanca Solidaria."
    });

    opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token JWT retornado por /api/v1/auth/login."
    });

    opcoes.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer")] = []
    });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres")!, name: "postgres", tags: ["ready"]);

builder.Services.AddCors(opcoes => opcoes.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<TratamentoDeErrosMiddleware>();
app.UseCors();
app.UseHttpMetrics();

app.UseSwagger();
app.UseSwaggerUI(opcoes =>
{
    opcoes.SwaggerEndpoint("/swagger/v1/swagger.json", "Conexao Solidaria v1");
    opcoes.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCampanhasEndpoints();
app.MapPainelPublicoEndpoints();
app.MapDoacoesEndpoints();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registro => registro.Tags.Contains("ready")
});
app.MapMetrics("/metrics");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await InfraestruturaInicializador.PrepararAsync(app.Services, aplicarMigrations: true);

app.Run();

public partial class Program;
