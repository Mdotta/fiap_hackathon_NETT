using Solidary.Domain.Enums;

namespace Solidary.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Cpf { get; set; }
    public DateTime CreatedAt { get; set; }
}
