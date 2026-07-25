using MediatR;
using Solidary.Application.Common;

namespace Solidary.Application.UseCases.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Role);
