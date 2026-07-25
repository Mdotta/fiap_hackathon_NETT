using Prometheus;
using Solidary.Api;
using Solidary.Api.Endpoints;
using Solidary.Application;
using Solidary.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapObservabilityEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program;
