using Microsoft.EntityFrameworkCore;
using Solidary.Domain.Enums;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Api.BackgroundJobs;

// Hangfire recurring job — periodically closes campaigns whose EndDate has elapsed while still Active.
public class CloseExpiredCampaignsJob(SolidaryDbContext dbContext, ILogger<CloseExpiredCampaignsJob> logger)
{
    public const string JobId = "close-expired-campaigns";
    public const string CronExpression = "*/5 * * * *";

    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var expiredCampaigns = await dbContext.Campaigns
            .Where(c => c.Status == CampaignStatus.Active && c.EndDate <= now)
            .ToListAsync();

        if (expiredCampaigns.Count == 0)
            return;

        foreach (var campaign in expiredCampaigns)
        {
            campaign.Complete();
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation("Closed {Count} expired campaign(s)", expiredCampaigns.Count);
    }
}
