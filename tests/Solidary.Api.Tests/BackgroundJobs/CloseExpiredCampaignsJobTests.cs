using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Solidary.Api.BackgroundJobs;
using Solidary.Api.Tests.TestSupport;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;

namespace Solidary.Api.Tests.BackgroundJobs;

public class CloseExpiredCampaignsJobTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task ExecuteAsync_ClosesActiveCampaignsPastEndDate()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();

        // Campaign.Create rejects an end date in the past, so build one that's already expired
        // by constructing it with a near-future end date, then simulate elapsed time via reflection-free
        // approach: create with a 1ms window in the future and wait it out.
        var expiring = Campaign.Create("Flash Drive", "Short-lived", DateTime.UtcNow, DateTime.UtcNow.AddMilliseconds(50), 1000m, AdminId);
        var stillActive = Campaign.Create("Long Drive", "Runs for a month", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 1000m, AdminId);

        dbContext.Campaigns.AddRange(expiring, stillActive);
        await dbContext.SaveChangesAsync();

        await Task.Delay(100);

        var job = new CloseExpiredCampaignsJob(dbContext, NullLogger<CloseExpiredCampaignsJob>.Instance);
        await job.ExecuteAsync();

        dbContext.Campaigns.Single(c => c.Id == expiring.Id).Status.Should().Be(CampaignStatus.Completed);
        dbContext.Campaigns.Single(c => c.Id == stillActive.Id).Status.Should().Be(CampaignStatus.Active);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoExpiredCampaigns_DoesNothing()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = Campaign.Create("Long Drive", "Runs for a month", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 1000m, AdminId);
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var job = new CloseExpiredCampaignsJob(dbContext, NullLogger<CloseExpiredCampaignsJob>.Instance);
        await job.ExecuteAsync();

        dbContext.Campaigns.Single().Status.Should().Be(CampaignStatus.Active);
    }
}
