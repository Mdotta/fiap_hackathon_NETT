using FluentAssertions;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Campaigns.ListActive;
using Solidary.Domain.Entities;

namespace Solidary.Api.Tests.UseCases.Campaigns.ListActive;

public class ListActiveCampaignsQueryHandlerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ReturnsOnlyActiveCampaignsWithExpectedFields()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();

        var active = Campaign.Create("Winter Coat Drive", "Coats for kids", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 5000m, AdminId);
        active.ApplyDonation(150m);

        var cancelled = Campaign.Create("Old Drive", "No longer running", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 1000m, AdminId);
        cancelled.Cancel();

        dbContext.Campaigns.AddRange(active, cancelled);
        await dbContext.SaveChangesAsync();

        var handler = new ListActiveCampaignsQueryHandler(dbContext);
        var result = await handler.Handle(new ListActiveCampaignsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].CampaignId.Should().Be(active.Id);
        result[0].Title.Should().Be("Winter Coat Drive");
        result[0].FundingGoal.Should().Be(5000m);
        result[0].TotalRaised.Should().Be(150m);
    }

    [Fact]
    public async Task Handle_WithNoActiveCampaigns_ReturnsEmptyList()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new ListActiveCampaignsQueryHandler(dbContext);

        var result = await handler.Handle(new ListActiveCampaignsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
