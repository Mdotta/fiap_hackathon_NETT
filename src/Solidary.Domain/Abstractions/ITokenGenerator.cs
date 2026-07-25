using Solidary.Domain.Entities;

namespace Solidary.Domain.Abstractions;

public interface ITokenGenerator
{
    TokenResult GenerateToken(User user);
}

public record TokenResult(string Token, DateTime ExpiresAtUtc);
