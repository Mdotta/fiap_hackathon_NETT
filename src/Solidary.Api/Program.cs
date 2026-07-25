using Prometheus;
using Solidary.Api;
using Solidary.Api.BackgroundJobs;
using Solidary.Api.Endpoints;
using Solidary.Application;
using Solidary.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

await app.ApplyMigrationsAsync();
app.ScheduleRecurringJobs();

app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApiEndpoints();
app.MapObservabilityEndpoints();
app.MapAuthEndpoints();
app.MapCampaignEndpoints();

app.Run();

public partial class Program;
