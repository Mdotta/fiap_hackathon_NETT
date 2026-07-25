using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Solidary.Domain.Entities;

namespace Solidary.Infrastructure.Persistence.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("Campaigns");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).IsRequired();
        builder.Property(c => c.FundingGoal).HasColumnType("numeric(18,2)");
        builder.Property(c => c.TotalRaised).HasColumnType("numeric(18,2)");
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
