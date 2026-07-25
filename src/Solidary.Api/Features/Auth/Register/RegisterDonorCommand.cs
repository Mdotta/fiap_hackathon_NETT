using MediatR;
using Solidary.Api.Common;

namespace Solidary.Api.Features.Auth.Register;

public record RegisterDonorCommand(string FullName, string Email, string Cpf, string Password)
    : IRequest<Result<RegisterDonorResponse>>;

public record RegisterDonorResponse(Guid UserId, string FullName, string Email);
