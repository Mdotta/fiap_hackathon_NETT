using MediatR;
using Solidary.Application.Common;

namespace Solidary.Application.UseCases.Campaigns.Cancel;

public record CancelCampaignCommand(Guid CampaignId) : IRequest<Result<CancelCampaignResponse>>;

public record CancelCampaignResponse(Guid CampaignId, string Status);
