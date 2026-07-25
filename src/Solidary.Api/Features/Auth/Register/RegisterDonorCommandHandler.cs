using MediatR;
using Microsoft.EntityFrameworkCore;
using Solidary.Api.Common;
using Solidary.Domain.Abstractions;
using Solidary.Domain.Entities;
using Solidary.Domain.Enums;
using Solidary.Domain.ValueObjects;
using Solidary.Infrastructure.Persistence;

namespace Solidary.Api.Features.Auth.Register;

public class RegisterDonorCommandHandler(SolidaryDbContext dbContext, IPasswordHasher passwordHasher)
    : IRequestHandler<RegisterDonorCommand, Result<RegisterDonorResponse>>
{
    public async Task<Result<RegisterDonorResponse>> Handle(RegisterDonorCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            return Result<RegisterDonorResponse>.Failure("Full name is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result<RegisterDonorResponse>.Failure("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Result<RegisterDonorResponse>.Failure("Password must be at least 8 characters long.");

        if (!CpfValidator.IsValid(request.Cpf))
            return Result<RegisterDonorResponse>.Failure("CPF is invalid.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailAlreadyExists = await dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyExists)
            return Result<RegisterDonorResponse>.Failure("Email is already registered.");

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

        return Result<RegisterDonorResponse>.Success(new RegisterDonorResponse(user.Id, user.FullName, user.Email));
    }
}
