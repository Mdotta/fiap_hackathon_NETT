using System.Text.RegularExpressions;

namespace Solidary.Domain.ValueObjects;

public static partial class CpfValidator
{
    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        var digits = DigitsOnlyRegex().Replace(cpf, string.Empty);

        if (digits.Length != 11 || digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        var firstCheckDigit = ComputeCheckDigit(numbers, 9);
        if (firstCheckDigit != numbers[9])
            return false;

        var secondCheckDigit = ComputeCheckDigit(numbers, 10);
        return secondCheckDigit == numbers[10];
    }

    private static int ComputeCheckDigit(int[] numbers, int length)
    {
        var sum = 0;
        var multiplier = length + 1;

        for (var i = 0; i < length; i++)
            sum += numbers[i] * (multiplier - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();
}
