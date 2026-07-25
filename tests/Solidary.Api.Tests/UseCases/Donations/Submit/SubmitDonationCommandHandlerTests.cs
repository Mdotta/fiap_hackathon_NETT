using FluentAssertions;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Donations.Submit;
using Solidary.Contracts;
using Solidary.Contracts.Events;
using Solidary.Domain.Entities;
using Xunit;

namespace Solidary.Api.Tests.UseCases.Donations.Submit;

public class SubmitDonationCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid DonorId = Guid.NewGuid();

    private static Campaign CreateActiveCampaign() =>
        Campaign.Create("Winter Coat Drive", "Coats for kids", DateTime.UtcNow, DateTime.UtcNow.AddDays(30), 5000m, AdminId);

    [Fact]
    public async Task Handle_WithValidDonation_PersistsDonationAndPublishesEvent()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = CreateActiveCampaign();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeEventPublisher();
        var handler = new SubmitDonationCommandHandler(dbContext, publisher);

        var command = new SubmitDonationCommand(DonorId, campaign.Id, 100m);
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Pending");
        dbContext.Donations.Should().Contain(d => d.CampaignId == campaign.Id && d.Amount == 100m);

        publisher.PublishedEvents.Should().ContainSingle();
        var (topic, key, publishedEvent) = publisher.PublishedEvents[0];
        topic.Should().Be(KafkaTopics.DonationReceived);
        key.Should().Be(campaign.Id.ToString());
        var donationEvent = publishedEvent.Should().BeOfType<ReceivedDonationEvent>().Subject;
        donationEvent.CampaignId.Should().Be(campaign.Id);
        donationEvent.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Handle_WithNonPositiveAmount_ReturnsFailureAndDoesNotPublish()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = CreateActiveCampaign();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeEventPublisher();
        var handler = new SubmitDonationCommandHandler(dbContext, publisher);

        var result = await handler.Handle(new SubmitDonationCommand(DonorId, campaign.Id, 0m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Donation amount must be greater than zero.");
        publisher.PublishedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithUnknownCampaign_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var publisher = new FakeEventPublisher();
        var handler = new SubmitDonationCommandHandler(dbContext, publisher);

        var result = await handler.Handle(new SubmitDonationCommand(DonorId, Guid.NewGuid(), 100m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Campaign not found.");
        publisher.PublishedEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithCancelledCampaign_ReturnsFailureAndDoesNotPublish()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var campaign = CreateActiveCampaign();
        campaign.Cancel();
        dbContext.Campaigns.Add(campaign);
        await dbContext.SaveChangesAsync();

        var publisher = new FakeEventPublisher();
        var handler = new SubmitDonationCommandHandler(dbContext, publisher);

        var result = await handler.Handle(new SubmitDonationCommand(DonorId, campaign.Id, 100m), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Donations cannot be made to a completed or cancelled campaign.");
        publisher.PublishedEvents.Should().BeEmpty();
    }
}
