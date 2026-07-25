using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;

namespace Solidary.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public static readonly Guid SeededAdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Cpf).HasMaxLength(14);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.HasData(new User
        {
            Id = SeededAdminId,
            FullName = "Platform Admin",
            Email = "admin@solidary.local",
            // BCrypt hash of "Admin@123" — local/dev seed only, documented in README.
            PasswordHash = "$2a$11$igJ/qwcg95//ANpKHmvlS.GVuAtOktGJ3nijaTGStJuC3jsyXGT22",
            Role = UserRole.Admin,
            Cpf = null,
            CreatedAt = SeededAt
        });
    }
}
