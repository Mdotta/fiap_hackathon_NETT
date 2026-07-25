using Microsoft.AspNetCore.Builder;
using Prometheus;
using Solidary.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<Worker>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpMetrics();

app.MapHealthChecks("/health");
app.MapMetrics("/metrics");

app.Run();
