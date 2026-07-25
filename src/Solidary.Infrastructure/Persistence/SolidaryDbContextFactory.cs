using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Solidary.Infrastructure.Persistence;

// Used by `dotnet ef migrations add` / `dotnet ef database update` at design time only.
// Runtime configuration (Api/Worker) wires the connection string via DI from appsettings/env vars.
public class SolidaryDbContextFactory : IDesignTimeDbContextFactory<SolidaryDbContext>
{
    public SolidaryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SOLIDARY_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=solidary;Username=solidary;Password=solidary";

        var optionsBuilder = new DbContextOptionsBuilder<SolidaryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SolidaryDbContext(optionsBuilder.Options);
    }
}
