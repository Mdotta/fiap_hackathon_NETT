using MediatR;

namespace Solidary.Application.UseCases.Campaigns.ListActive;

public record ListActiveCampaignsQuery : IRequest<List<ActiveCampaignResponse>>;

public record ActiveCampaignResponse(Guid CampaignId, string Title, decimal FundingGoal, decimal TotalRaised);
