using MediatR;
using Microsoft.Extensions.Logging;
using Solidary.Application.Common;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Campaigns.Cancel;

public class CancelCampaignCommandHandler(SolidaryDbContext dbContext, ILogger<CancelCampaignCommandHandler> logger)
    : IRequestHandler<CancelCampaignCommand, Result<CancelCampaignResponse>>
{
    public async Task<Result<CancelCampaignResponse>> Handle(CancelCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Campaigns.FindAsync([request.CampaignId], cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning("Cancel rejected: campaign {CampaignId} not found", request.CampaignId);
            return Result<CancelCampaignResponse>.Failure("Campaign not found.");
        }

        if (!campaign.CanReceiveDonations)
        {
            logger.LogWarning(
                "Cancel rejected: campaign {CampaignId} is already {Status}", campaign.Id, campaign.Status);
            return Result<CancelCampaignResponse>.Failure("Only active campaigns can be cancelled.");
        }

        campaign.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Campaign {CampaignId} '{Title}' cancelled", campaign.Id, campaign.Title);

        return Result<CancelCampaignResponse>.Success(new CancelCampaignResponse(campaign.Id, campaign.Status.ToString()));
    }
}
