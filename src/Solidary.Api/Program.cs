using Prometheus;
using Solidary.Api;
using Solidary.Api.BackgroundJobs;
using Solidary.Api.Endpoints;
using Solidary.Api.Middlewares;
using Solidary.Application;
using Solidary.Infrastructure;
using Solidary.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Host.UseSolidarySerilog("solidary-api");

var app = builder.Build();

await app.ApplyMigrationsAsync();
app.ScheduleRecurringJobs();

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SerilogUserContextMiddleware>();

app.MapOpenApiEndpoints();
app.MapObservabilityEndpoints();
app.MapAuthEndpoints();
app.MapCampaignEndpoints();

app.Run();

public partial class Program;
