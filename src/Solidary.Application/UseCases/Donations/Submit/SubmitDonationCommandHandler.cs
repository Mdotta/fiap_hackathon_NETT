using MediatR;
using Solidary.Application.Common;
using Solidary.Contracts;
using Solidary.Contracts.Events;
using Solidary.Domain.Abstractions;
using Solidary.Domain.Entities;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Donations.Submit;

public class SubmitDonationCommandHandler(SolidaryDbContext dbContext, IEventPublisher eventPublisher)
    : IRequestHandler<SubmitDonationCommand, Result<SubmitDonationResponse>>
{
    public async Task<Result<SubmitDonationResponse>> Handle(SubmitDonationCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
            return Result<SubmitDonationResponse>.Failure("Donation amount must be greater than zero.");

        var campaign = await dbContext.Campaigns.FindAsync([request.CampaignId], cancellationToken);
        if (campaign is null)
            return Result<SubmitDonationResponse>.Failure("Campaign not found.");

        if (!campaign.CanReceiveDonations)
            return Result<SubmitDonationResponse>.Failure("Donations cannot be made to a completed or cancelled campaign.");

        var donation = Donation.Create(campaign, request.DonorId, request.Amount);

        dbContext.Donations.Add(donation);
        await dbContext.SaveChangesAsync(cancellationToken);

        var donationEvent = new ReceivedDonationEvent(
            donation.Id,
            donation.CampaignId,
            donation.DonorId,
            donation.Amount,
            DateTime.UtcNow);

        await eventPublisher.PublishAsync(KafkaTopics.DonationReceived, donation.CampaignId.ToString(), donationEvent, cancellationToken);

        return Result<SubmitDonationResponse>.Success(new SubmitDonationResponse(donation.Id, donation.Status.ToString()));
    }
}
