using Microsoft.AspNetCore.Builder;
using Prometheus;
using Solidary.Infrastructure;
using Solidary.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddWorker();

var app = builder.Build();

app.UseHttpMetrics();

app.MapHealthChecks("/health");
app.MapMetrics("/metrics");

app.Run();
