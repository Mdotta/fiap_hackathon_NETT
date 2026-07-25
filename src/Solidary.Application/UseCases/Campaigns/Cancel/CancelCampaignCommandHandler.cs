using MediatR;
using Solidary.Application.Common;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Campaigns.Cancel;

public class CancelCampaignCommandHandler(SolidaryDbContext dbContext)
    : IRequestHandler<CancelCampaignCommand, Result<CancelCampaignResponse>>
{
    public async Task<Result<CancelCampaignResponse>> Handle(CancelCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await dbContext.Campaigns.FindAsync([request.CampaignId], cancellationToken);
        if (campaign is null)
            return Result<CancelCampaignResponse>.Failure("Campaign not found.");

        if (!campaign.CanReceiveDonations)
            return Result<CancelCampaignResponse>.Failure("Only active campaigns can be cancelled.");

        campaign.Cancel();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CancelCampaignResponse>.Success(new CancelCampaignResponse(campaign.Id, campaign.Status.ToString()));
    }
}
