using MediatR;
using Microsoft.EntityFrameworkCore;
using Solidary.Application.Common;
using Solidary.Domain.Abstractions;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Auth.Login;

public class LoginCommandHandler(SolidaryDbContext dbContext, IPasswordHasher passwordHasher, ITokenGenerator tokenGenerator)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private const string InvalidCredentialsError = "Invalid email or password.";

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure(InvalidCredentialsError);

        var token = tokenGenerator.GenerateToken(user);

        return Result<LoginResponse>.Success(new LoginResponse(token.Token, token.ExpiresAtUtc, user.Role.ToString()));
    }
}
