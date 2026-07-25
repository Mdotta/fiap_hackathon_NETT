using Microsoft.EntityFrameworkCore;
using Solidary.Domain.Entities;

namespace Solidary.Infrastructure.Persistence;

public class SolidaryDbContext(DbContextOptions<SolidaryDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Donation> Donations => Set<Donation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SolidaryDbContext).Assembly);
    }
}
