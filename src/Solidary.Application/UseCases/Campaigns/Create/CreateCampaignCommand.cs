using MediatR;
using Solidary.Application.Common;

namespace Solidary.Application.UseCases.Campaigns.Create;

public record CreateCampaignCommand(
    string Title,
    string Description,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    decimal FundingGoal,
    Guid CreatedByUserId) : IRequest<Result<CreateCampaignResponse>>;

public record CreateCampaignResponse(Guid CampaignId, string Title, string Status);
