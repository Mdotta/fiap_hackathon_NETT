using FluentAssertions;
using Prometheus;
using Solidary.Worker.Metrics;

namespace Solidary.Api.Tests.Metrics;

public class DonationMetricsTests
{
    [Fact]
    public void RecordDonation_IncrementsCountAndAmountForCampaign()
    {
        var metrics = new DonationMetrics(new CollectorRegistry());
        var campaignId = Guid.NewGuid();

        metrics.RecordDonation(campaignId, "Winter Coat Drive", 100m);
        metrics.RecordDonation(campaignId, "Winter Coat Drive", 50.25m);

        metrics.GetProcessedCount(campaignId, "Winter Coat Drive").Should().Be(2);
        metrics.GetTotalAmount(campaignId, "Winter Coat Drive").Should().Be(150.25d);
    }

    [Fact]
    public void RecordDonation_KeepsDifferentCampaignsIndependent()
    {
        var metrics = new DonationMetrics(new CollectorRegistry());
        var campaignA = Guid.NewGuid();
        var campaignB = Guid.NewGuid();

        metrics.RecordDonation(campaignA, "Campaign A", 100m);
        metrics.RecordDonation(campaignB, "Campaign B", 25m);

        metrics.GetTotalAmount(campaignA, "Campaign A").Should().Be(100d);
        metrics.GetTotalAmount(campaignB, "Campaign B").Should().Be(25d);
    }
}
