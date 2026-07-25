using Microsoft.EntityFrameworkCore;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Api;

public static class DatabaseMigrator
{
    // Applies pending EF Core migrations (and the seeded Admin HasData row) on startup, so
    // `docker compose --profile app up` works cold with no separate `dotnet ef database update` step.
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SolidaryDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
