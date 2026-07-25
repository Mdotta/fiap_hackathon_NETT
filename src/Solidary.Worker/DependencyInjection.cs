using Solidary.Worker.Consumers;

namespace Solidary.Worker;

public static class DependencyInjection
{
    public static IServiceCollection AddWorker(this IServiceCollection services)
    {
        services.AddHostedService<DonationEventConsumer>();
        services.AddHealthChecks();

        return services;
    }
}
