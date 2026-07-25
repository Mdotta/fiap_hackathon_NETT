using FluentAssertions;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Campaigns.Create;
using Xunit;

namespace Solidary.Api.Tests.UseCases.Campaigns.Create;

public class CreateCampaignCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WithValidData_CreatesCampaignAndReturnsSuccess()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new CreateCampaignCommandHandler(dbContext);

        var command = new CreateCampaignCommand(
            "Winter Coat Drive",
            "Collecting coats for children in shelters",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            5000m,
            AdminId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Active");
        dbContext.Campaigns.Should().Contain(c => c.Title == "Winter Coat Drive");
    }

    [Fact]
    public async Task Handle_WithPastEndDate_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new CreateCampaignCommandHandler(dbContext);

        var command = new CreateCampaignCommand(
            "Winter Coat Drive",
            "Collecting coats for children in shelters",
            DateTime.UtcNow.AddDays(-10),
            DateTime.UtcNow.AddDays(-1),
            5000m,
            AdminId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("End date cannot be in the past.");
    }

    [Fact]
    public async Task Handle_WithNonPositiveFundingGoal_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new CreateCampaignCommandHandler(dbContext);

        var command = new CreateCampaignCommand(
            "Winter Coat Drive",
            "Collecting coats for children in shelters",
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            0m,
            AdminId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Funding goal must be greater than zero.");
    }
}
