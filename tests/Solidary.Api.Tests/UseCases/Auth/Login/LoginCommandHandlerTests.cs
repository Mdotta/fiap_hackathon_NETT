using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Solidary.Api.Tests.TestSupport;
using Solidary.Application.UseCases.Auth.Login;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;
using Solidary.Infrastructure.Auth;
using Xunit;

namespace Solidary.Api.Tests.UseCases.Auth.Login;

public class LoginCommandHandlerTests
{
    private static readonly JwtSettings Settings = new()
    {
        Issuer = "Solidary.Api.Tests",
        Audience = "Solidary.Api.Tests",
        SigningKey = "test-signing-key-not-for-production-use-1234567890",
        ExpirationMinutes = 30
    };

    [Fact]
    public async Task Handle_WithValidCredentials_ReturnsTokenAndRole()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Maria Silva",
            Email = "maria@example.com",
            PasswordHash = hasher.Hash("SecurePass1"),
            Role = UserRole.Donor,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(dbContext, hasher, new JwtTokenGenerator(Options.Create(Settings)), NullLogger<LoginCommandHandler>.Instance);
        var result = await handler.Handle(new LoginCommand("maria@example.com", "SecurePass1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().NotBeNullOrWhiteSpace();
        result.Value.Role.Should().Be("Donor");
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Maria Silva",
            Email = "maria@example.com",
            PasswordHash = hasher.Hash("SecurePass1"),
            Role = UserRole.Donor,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = new LoginCommandHandler(dbContext, hasher, new JwtTokenGenerator(Options.Create(Settings)), NullLogger<LoginCommandHandler>.Instance);
        var result = await handler.Handle(new LoginCommand("maria@example.com", "WrongPassword"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Handle_WithUnknownEmail_ReturnsFailure()
    {
        await using var dbContext = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();

        var handler = new LoginCommandHandler(dbContext, hasher, new JwtTokenGenerator(Options.Create(Settings)), NullLogger<LoginCommandHandler>.Instance);
        var result = await handler.Handle(new LoginCommand("ghost@example.com", "Whatever1"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password.");
    }
}
