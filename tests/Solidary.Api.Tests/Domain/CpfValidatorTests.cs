using FluentAssertions;
using Solidary.Domain.ValueObjects;
using Xunit;

namespace Solidary.Api.Tests.Domain;

public class CpfValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    public void IsValid_WithValidCpf_ReturnsTrue(string cpf)
    {
        CpfValidator.IsValid(cpf).Should().BeTrue();
    }

    [Theory]
    [InlineData("11111111111")]
    [InlineData("12345678900")]
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_WithInvalidCpf_ReturnsFalse(string? cpf)
    {
        CpfValidator.IsValid(cpf).Should().BeFalse();
    }
}
