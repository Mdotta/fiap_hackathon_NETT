using Solidary.Domain.Enums;

namespace Solidary.Domain.Entities;

public class Donation
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid DonorId { get; private set; }
    public decimal Amount { get; private set; }
    public DonationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    private Donation() { }

    public static Donation Create(Campaign campaign, Guid donorId, decimal amount)
    {
        if (!campaign.CanReceiveDonations)
            throw new InvalidOperationException("Donations cannot be made to a completed or cancelled campaign.");

        if (amount <= 0)
            throw new ArgumentException("Donation amount must be greater than zero.", nameof(amount));

        return new Donation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            DonorId = donorId,
            Amount = amount,
            Status = DonationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkProcessed()
    {
        Status = DonationStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        Status = DonationStatus.Failed;
        ProcessedAt = DateTime.UtcNow;
    }
}
