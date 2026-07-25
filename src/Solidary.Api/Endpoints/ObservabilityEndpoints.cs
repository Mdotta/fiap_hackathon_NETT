using Prometheus;

namespace Solidary.Api.Endpoints;

public static class ObservabilityEndpoints
{
    public static IEndpointRouteBuilder MapObservabilityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health");
        app.MapMetrics("/metrics");

        return app;
    }
}
