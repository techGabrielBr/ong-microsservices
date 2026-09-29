using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpMetrics();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapMetrics("/metrics");

// Ocelot e um middleware terminal: sem este desvio ele responderia 404 para os
// proprios endpoints de saude e metricas do gateway.
app.MapWhen(
    contexto => !contexto.Request.Path.StartsWithSegments("/metrics")
             && !contexto.Request.Path.StartsWithSegments("/health"),
    ramo => ramo.UseOcelot().GetAwaiter().GetResult());

app.Run();
