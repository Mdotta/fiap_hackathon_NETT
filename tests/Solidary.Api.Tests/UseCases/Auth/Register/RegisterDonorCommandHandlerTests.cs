using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Auth.Register;
using Solidary.Infrastructure.Auth;
using Xunit;

namespace Solidary.Api.Tests.UseCases.Auth.Register;

public class RegisterDonorCommandHandlerTests
{
    private const string ValidCpf = "52998224725";

    [Fact]
    public async Task Handle_WithValidData_CreatesDonorAndReturnsSuccess()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new RegisterDonorCommandHandler(dbContext, new BCryptPasswordHasher(), NullLogger<RegisterDonorCommandHandler>.Instance);

        var command = new RegisterDonorCommand("Maria Silva", "maria@example.com", ValidCpf, "SecurePass1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("maria@example.com");
        dbContext.Users.Should().Contain(u => u.Email == "maria@example.com");
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new RegisterDonorCommandHandler(dbContext, new BCryptPasswordHasher(), NullLogger<RegisterDonorCommandHandler>.Instance);
        var command = new RegisterDonorCommand("Maria Silva", "maria@example.com", ValidCpf, "SecurePass1");
        await handler.Handle(command, CancellationToken.None);

        var duplicate = new RegisterDonorCommand("Maria Copy", "MARIA@example.com", ValidCpf, "SecurePass1");
        var result = await handler.Handle(duplicate, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Email is already registered.");
    }

    [Fact]
    public async Task Handle_WithInvalidCpf_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new RegisterDonorCommandHandler(dbContext, new BCryptPasswordHasher(), NullLogger<RegisterDonorCommandHandler>.Instance);

        var command = new RegisterDonorCommand("Maria Silva", "maria@example.com", "11111111111", "SecurePass1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("CPF is invalid.");
    }

    [Fact]
    public async Task Handle_WithShortPassword_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var handler = new RegisterDonorCommandHandler(dbContext, new BCryptPasswordHasher(), NullLogger<RegisterDonorCommandHandler>.Instance);

        var command = new RegisterDonorCommand("Maria Silva", "maria@example.com", ValidCpf, "short");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Password must be at least 8 characters long.");
    }
}
