using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Solidary.Domain.Abstractions;
using Solidary.Infrastructure.Auth;
using Solidary.Infrastructure.Messaging;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SolidaryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<KafkaSettings>(configuration.GetSection(KafkaSettings.SectionName));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

        return services;
    }
}
