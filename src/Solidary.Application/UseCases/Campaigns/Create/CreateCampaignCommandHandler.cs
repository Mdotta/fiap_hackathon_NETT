using MediatR;
using Microsoft.Extensions.Logging;
using Solidary.Application.Common;
using Solidary.Domain.Entities;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Campaigns.Create;

public class CreateCampaignCommandHandler(SolidaryDbContext dbContext, ILogger<CreateCampaignCommandHandler> logger)
    : IRequestHandler<CreateCampaignCommand, Result<CreateCampaignResponse>>
{
    public async Task<Result<CreateCampaignResponse>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Fail("Title is required.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return Fail("Description is required.");

        if (request.EndDate <= DateTimeOffset.UtcNow)
            return Fail("End date cannot be in the past.");

        if (request.FundingGoal <= 0)
            return Fail("Funding goal must be greater than zero.");

        var campaign = Campaign.Create(
            request.Title.Trim(),
            request.Description.Trim(),
            request.StartDate,
            request.EndDate,
            request.FundingGoal,
            request.CreatedByUserId);

        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Campaign {CampaignId} '{Title}' created by {UserId} with funding goal {FundingGoal}",
            campaign.Id, campaign.Title, request.CreatedByUserId, campaign.FundingGoal);

        return Result<CreateCampaignResponse>.Success(
            new CreateCampaignResponse(campaign.Id, campaign.Title, campaign.Status.ToString()));
    }

    private Result<CreateCampaignResponse> Fail(string error)
    {
        logger.LogWarning("Campaign creation rejected: {Reason}", error);
        return Result<CreateCampaignResponse>.Failure(error);
    }
}
