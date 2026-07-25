using MediatR;
using Solidary.Application.Common;
using Solidary.Domain.Entities;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Campaigns.Create;

public class CreateCampaignCommandHandler(SolidaryDbContext dbContext)
    : IRequestHandler<CreateCampaignCommand, Result<CreateCampaignResponse>>
{
    public async Task<Result<CreateCampaignResponse>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<CreateCampaignResponse>.Failure("Title is required.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<CreateCampaignResponse>.Failure("Description is required.");

        if (request.EndDate <= DateTime.UtcNow)
            return Result<CreateCampaignResponse>.Failure("End date cannot be in the past.");

        if (request.FundingGoal <= 0)
            return Result<CreateCampaignResponse>.Failure("Funding goal must be greater than zero.");

        var campaign = Campaign.Create(
            request.Title.Trim(),
            request.Description.Trim(),
            request.StartDate,
            request.EndDate,
            request.FundingGoal,
            request.CreatedByUserId);

        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CreateCampaignResponse>.Success(
            new CreateCampaignResponse(campaign.Id, campaign.Title, campaign.Status.ToString()));
    }
}
