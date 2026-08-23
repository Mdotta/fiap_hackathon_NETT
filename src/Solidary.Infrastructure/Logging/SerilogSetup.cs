using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace Solidary.Infrastructure.Logging;

public static class SerilogSetup
{
    public static IHostBuilder UseSolidarySerilog(this IHostBuilder hostBuilder, string serviceName)
    {
        return hostBuilder.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .WriteTo.Console()
                .WriteTo.GrafanaLoki(
                    "http://localhost:3100",
                    [new LokiLabel { Key = "app", Value = "solidary" }])
                .Enrich.WithProperty("service", serviceName);
        });
    }
}