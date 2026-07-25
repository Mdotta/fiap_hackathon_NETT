namespace Solidary.Contracts.Events;

public record ReceivedDonationEvent(
    Guid DonationId,
    Guid CampaignId,
    Guid DonorId,
    decimal Amount,
    DateTime OccurredAt);
