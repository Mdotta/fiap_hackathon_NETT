using MediatR;
using Solidary.Application.Common;

namespace Solidary.Application.UseCases.Auth.Register;

public record RegisterDonorCommand(string FullName, string Email, string Cpf, string Password)
    : IRequest<Result<RegisterDonorResponse>>;

public record RegisterDonorResponse(Guid UserId, string FullName, string Email);
