using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solidary.Application.Common;
using Solidary.Domain.Abstractions;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Auth.Login;

public class LoginCommandHandler(SolidaryDbContext dbContext, IPasswordHasher passwordHasher, ITokenGenerator tokenGenerator, ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private const string InvalidCredentialsError = "Invalid email or password.";

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Login failed for {Email}: invalid credentials", normalizedEmail);
            return Result<LoginResponse>.Failure(InvalidCredentialsError);
        }

        var token = tokenGenerator.GenerateToken(user);

        logger.LogInformation("Login successful for {UserId} ({Email}) with role {Role}", user.Id, user.Email, user.Role);

        return Result<LoginResponse>.Success(new LoginResponse(token.Token, token.ExpiresAtUtc, user.Role.ToString()));
    }
}
