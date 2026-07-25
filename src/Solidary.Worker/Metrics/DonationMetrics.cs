using Prometheus;

namespace Solidary.Worker.Metrics;

// Custom application metrics (on top of prometheus-net's default HTTP/process metrics), recorded once
// a donation event has been durably applied — never on retries of an already-processed donation.
public class DonationMetrics
{
    private readonly Counter _donationsProcessedTotal;
    private readonly Counter _donationAmountTotal;

    // Uses the global default registry in production (so prometheus-net's /metrics endpoint picks it up);
    // tests can pass an isolated registry to avoid cross-test pollution of shared static state.
    public DonationMetrics() : this(Prometheus.Metrics.DefaultRegistry)
    {
    }

    public DonationMetrics(CollectorRegistry registry)
    {
        var factory = Prometheus.Metrics.WithCustomRegistry(registry);

        _donationsProcessedTotal = factory.CreateCounter(
            "solidary_donations_processed_total",
            "Total number of donations processed by campaign.",
            new CounterConfiguration { LabelNames = ["campaign_id", "campaign_title"] });

        _donationAmountTotal = factory.CreateCounter(
            "solidary_donation_amount_total",
            "Total amount donated (processed) by campaign, in the campaign's currency unit.",
            new CounterConfiguration { LabelNames = ["campaign_id", "campaign_title"] });
    }

    public void RecordDonation(Guid campaignId, string campaignTitle, decimal amount)
    {
        _donationsProcessedTotal.WithLabels(campaignId.ToString(), campaignTitle).Inc();
        _donationAmountTotal.WithLabels(campaignId.ToString(), campaignTitle).Inc((double)amount);
    }

    public double GetProcessedCount(Guid campaignId, string campaignTitle) =>
        _donationsProcessedTotal.WithLabels(campaignId.ToString(), campaignTitle).Value;

    public double GetTotalAmount(Guid campaignId, string campaignTitle) =>
        _donationAmountTotal.WithLabels(campaignId.ToString(), campaignTitle).Value;
}
