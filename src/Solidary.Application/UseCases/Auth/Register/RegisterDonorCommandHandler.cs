using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Solidary.Application.Common;
using Solidary.Domain.Abstractions;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;
using Solidary.Domain.ValueObjects;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Application.UseCases.Auth.Register;

public class RegisterDonorCommandHandler(
    SolidaryDbContext dbContext,
    IPasswordHasher passwordHasher,
    ILogger<RegisterDonorCommandHandler> logger)
    : IRequestHandler<RegisterDonorCommand, Result<RegisterDonorResponse>>
{
    public async Task<Result<RegisterDonorResponse>> Handle(RegisterDonorCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Fail("Full name is required.", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email))
            return Fail("Email is required.", request.Email);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Fail("Password must be at least 8 characters long.", request.Email);

        if (!CpfValidator.IsValid(request.Cpf))
            return Fail("CPF is invalid.", request.Email);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyExists = await dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyExists)
            return Fail("Email is already registered.", normalizedEmail);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            Cpf = request.Cpf,
            Role = UserRole.Donor,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Donor {UserId} registered with email {Email}", user.Id, user.Email);

        return Result<RegisterDonorResponse>.Success(new RegisterDonorResponse(user.Id, user.FullName, user.Email));
    }

    private Result<RegisterDonorResponse> Fail(string error, string? email)
    {
        logger.LogWarning("Donor registration rejected for {Email}: {Reason}", email, error);
        return Result<RegisterDonorResponse>.Failure(error);
    }
}
