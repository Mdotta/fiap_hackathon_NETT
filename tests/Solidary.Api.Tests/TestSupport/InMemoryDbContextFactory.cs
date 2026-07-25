using Microsoft.EntityFrameworkCore;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Api.Tests.TestSupport;

public static class InMemoryDbContextFactory
{
    public static SolidaryDbContext Create()
    {
        var options = new DbContextOptionsBuilder<SolidaryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SolidaryDbContext(options);
    }
}
