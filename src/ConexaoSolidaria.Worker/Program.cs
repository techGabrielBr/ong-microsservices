using ConexaoSolidaria.Infrastructure;
using ConexaoSolidaria.Worker.Processing;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfraestrutura(builder.Configuration);
builder.Services.AddScoped<ProcessadorDeDoacoes>();
builder.Services.AddHostedService<ConsumidorDeDoacoes>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapMetrics("/metrics");
app.MapGet("/", () => Results.Ok(new { servico = "conexao-solidaria-worker", status = "online" }));

await InfraestruturaInicializador.PrepararAsync(app.Services, aplicarMigrations: false);

app.Run();
