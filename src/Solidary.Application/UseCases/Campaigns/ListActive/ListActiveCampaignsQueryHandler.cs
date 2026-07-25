using MediatR;
using Microsoft.EntityFrameworkCore;
using Solidary.Domain.Enums;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Campaigns.ListActive;

public class ListActiveCampaignsQueryHandler(SolidaryDbContext dbContext)
    : IRequestHandler<ListActiveCampaignsQuery, List<ActiveCampaignResponse>>
{
    public Task<List<ActiveCampaignResponse>> Handle(ListActiveCampaignsQuery request, CancellationToken cancellationToken)
    {
        return dbContext.Campaigns
            .Where(c => c.Status == CampaignStatus.Active)
            .OrderBy(c => c.EndDate)
            .Select(c => new ActiveCampaignResponse(c.Id, c.Title, c.FundingGoal, c.TotalRaised))
            .ToListAsync(cancellationToken);
    }
}
