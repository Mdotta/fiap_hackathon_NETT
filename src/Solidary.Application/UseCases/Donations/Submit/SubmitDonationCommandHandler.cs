using MediatR;
using Microsoft.Extensions.Logging;
using Solidary.Application.Common;
using Solidary.Contracts;
using Solidary.Contracts.Events;
using Solidary.Domain.Abstractions;
using Solidary.Domain.Entities;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Donations.Submit;

public class SubmitDonationCommandHandler(
    SolidaryDbContext dbContext,
    IEventPublisher eventPublisher,
    ILogger<SubmitDonationCommandHandler> logger)
    : IRequestHandler<SubmitDonationCommand, Result<SubmitDonationResponse>>
{
    public async Task<Result<SubmitDonationResponse>> Handle(SubmitDonationCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            logger.LogWarning(
                "Donation rejected for campaign {CampaignId}: amount {Amount} is not positive", request.CampaignId, request.Amount);
            return Result<SubmitDonationResponse>.Failure("Donation amount must be greater than zero.");
        }

        var campaign = await dbContext.Campaigns.FindAsync([request.CampaignId], cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning("Donation rejected: campaign {CampaignId} not found", request.CampaignId);
            return Result<SubmitDonationResponse>.Failure("Campaign not found.");
        }

        if (!campaign.CanReceiveDonations)
        {
            logger.LogWarning(
                "Donation rejected: campaign {CampaignId} is {Status}, cannot receive donations", campaign.Id, campaign.Status);
            return Result<SubmitDonationResponse>.Failure("Donations cannot be made to a completed or cancelled campaign.");
        }

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

        logger.LogInformation(
            "Donation {DonationId} of {Amount} submitted by {DonorId} to campaign {CampaignId}, event published to {Topic}",
            donation.Id, donation.Amount, donation.DonorId, campaign.Id, KafkaTopics.DonationReceived);

        return Result<SubmitDonationResponse>.Success(new SubmitDonationResponse(donation.Id, donation.Status.ToString()));
    }
}
