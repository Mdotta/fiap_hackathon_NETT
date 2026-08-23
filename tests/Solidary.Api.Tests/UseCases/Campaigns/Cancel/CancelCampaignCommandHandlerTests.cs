using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Campaigns.Cancel;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;

namespace Solidary.Api.Tests.UseCases.Campaigns.Cancel;

public class CancelCampaignCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private static Campaign CreateActiveCampaign() =>
        Campaign.Create("Winter Coat Drive", "Coats for kids", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 5000m, AdminId);

    [Fact]
    public async Task Handle_WithActiveCampaign_CancelsAndReturnsSuccess()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = CreateActiveCampaign();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var handler = new CancelCampaignCommandHandler(dbContext, NullLogger<CancelCampaignCommandHandler>.Instance);
        var result = await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Cancelled");
        dbContext.Campaigns.Single(c => c.Id == campaign.Id).Status.Should().Be(CampaignStatus.Cancelled);
    }

    [Fact]
    public async Task Handle_WithUnknownCampaign_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new CancelCampaignCommandHandler(dbContext, NullLogger<CancelCampaignCommandHandler>.Instance);

        var result = await handler.Handle(new CancelCampaignCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Campaign not found.");
    }

    [Fact]
    public async Task Handle_WithAlreadyCancelledCampaign_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = CreateActiveCampaign();
        campaign.Cancel();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var handler = new CancelCampaignCommandHandler(dbContext, NullLogger<CancelCampaignCommandHandler>.Instance);
        var result = await handler.Handle(new CancelCampaignCommand(campaign.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Only active campaigns can be cancelled.");
    }
}
