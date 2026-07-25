using Solidary.Domain.Enums;

namespace Solidary.Domain.Entities;

public class Campaign
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset StartDate { get; private set; }
    public DateTimeOffset EndDate { get; private set; }
    public decimal FundingGoal { get; private set; }
    public decimal TotalRaised { get; private set; }
    public CampaignStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private Campaign() { }

    public static Campaign Create(string title, string description, DateTimeOffset startDate, DateTimeOffset endDate, decimal fundingGoal, Guid createdByUserId)
    {
        if (endDate <= DateTimeOffset.UtcNow)
            throw new ArgumentException("A campaign's end date cannot be in the past.", nameof(endDate));

        if (fundingGoal <= 0)
            throw new ArgumentException("A campaign's funding goal must be greater than zero.", nameof(fundingGoal));

        return new Campaign
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            StartDate = startDate.ToUniversalTime(),
            EndDate = endDate.ToUniversalTime(),
            FundingGoal = fundingGoal,
            TotalRaised = 0,
            Status = CampaignStatus.Active,
            CreatedByUserId = createdByUserId
        };
    }

    public bool CanReceiveDonations => Status == CampaignStatus.Active;

    public void ApplyDonation(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Donation amount must be greater than zero.", nameof(amount));

        TotalRaised += amount;
    }

    public void Complete()
    {
        if (Status != CampaignStatus.Active)
            throw new InvalidOperationException("Only active campaigns can be completed.");

        Status = CampaignStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != CampaignStatus.Active)
            throw new InvalidOperationException("Only active campaigns can be cancelled.");

        Status = CampaignStatus.Cancelled;
    }
}
