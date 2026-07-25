using Hangfire;

namespace Solidary.Api.BackgroundJobs;

public static class BackgroundJobScheduler
{
    public static void ScheduleRecurringJobs(this WebApplication app)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<CloseExpiredCampaignsJob>(
            CloseExpiredCampaignsJob.JobId,
            job => job.ExecuteAsync(),
            CloseExpiredCampaignsJob.CronExpression);
    }
}
