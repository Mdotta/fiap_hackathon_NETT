using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Solidary.Domain.Entities;

namespace Solidary.Infrastructure.Persistence.Configurations;

public class DonationConfiguration : IEntityTypeConfiguration<Donation>
{
    public void Configure(EntityTypeBuilder<Donation> builder)
    {
        builder.ToTable("Donations");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Amount).HasColumnType("numeric(18,2)");
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Campaign>()
            .WithMany()
            .HasForeignKey(d => d.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.DonorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
