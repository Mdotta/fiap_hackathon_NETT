using Solidary.Worker.Consumers;
using Solidary.Worker.Metrics;

namespace Solidary.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddWorker(this IServiceCollection services)
    {
        services.AddHostedService<DonationEventConsumer>();
        services.AddHealthChecks();
        services.AddSingleton<DonationMetrics>();

        return services;
    }
}
