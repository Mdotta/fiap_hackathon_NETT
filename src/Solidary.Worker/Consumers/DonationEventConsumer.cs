using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Solidary.Contracts;
using Solidary.Contracts.Events;
using Solidary.Domain.Enums;
using Solidary.Infrastructure.Messaging;
using Solidary.Infrastructure.Persistence;
using Solidary.Worker.Metrics;

namespace Solidary.Worker.Consumers;

public class DonationEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaSettings> kafkaOptions,
    DonationMetrics donationMetrics,
    ILogger<DonationEventConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = kafkaOptions.Value;
        var config = new ConsumerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            GroupId = settings.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(KafkaTopics.DonationReceived);

        logger.LogInformation("Subscribed to {Topic} as consumer group {Group}", KafkaTopics.DonationReceived, settings.ConsumerGroup);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException ex)
            {
                logger.LogError(ex, "Error consuming from {Topic}", KafkaTopics.DonationReceived);
                continue;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result?.Message is null)
                continue;

            try
            {
                await ProcessMessageAsync(result.Message.Value, stoppingToken);
                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process donation event, offset will not be committed and the message will be retried");
            }
        }

        consumer.Close();
    }

    private async Task ProcessMessageAsync(string payload, CancellationToken cancellationToken)
    {
        var donationEvent = JsonSerializer.Deserialize<ReceivedDonationEvent>(payload);
        if (donationEvent is null)
        {
            logger.LogWarning("Received an unreadable donation event payload, skipping");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SolidaryDbContext>();

        var donation = await dbContext.Donations.FindAsync([donationEvent.DonationId], cancellationToken);
        if (donation is null)
        {
            logger.LogWarning("Donation {DonationId} not found, skipping", donationEvent.DonationId);
            return;
        }

        if (donation.Status == DonationStatus.Processed)
        {
            logger.LogInformation("Donation {DonationId} already processed, skipping (duplicate delivery)", donationEvent.DonationId);
            return;
        }

        var campaign = await dbContext.Campaigns.FindAsync([donationEvent.CampaignId], cancellationToken);
        if (campaign is null)
        {
            logger.LogWarning("Campaign {CampaignId} not found for donation {DonationId}, skipping", donationEvent.CampaignId, donationEvent.DonationId);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        campaign.ApplyDonation(donationEvent.Amount);
        donation.MarkProcessed();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        donationMetrics.RecordDonation(campaign.Id, campaign.Title, donationEvent.Amount);

        logger.LogInformation(
            "Processed donation {DonationId}: campaign {CampaignId} total raised is now {TotalRaised}",
            donation.Id, campaign.Id, campaign.TotalRaised);
    }
}
