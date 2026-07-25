using MediatR;
using Solidary.Api.Common;

namespace Solidary.Api.Features.Auth.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;

public record LoginResponse(string Token, DateTime ExpiresAtUtc, string Role);
