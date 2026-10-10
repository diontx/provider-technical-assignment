using System.ComponentModel.DataAnnotations;

namespace Candidate.API.Contracts;

/// <summary>
/// Validates that a string is the name of a member of the given enum (case-insensitive).
/// Numeric values such as "1" are rejected even though Enum.TryParse would accept them.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EnumNameAttribute(Type enumType) : ValidationAttribute
{
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return IsName(typeof(TEnum), value) && Enum.TryParse(value!.Trim(), ignoreCase: true, out result);
    }

    public override bool IsValid(object? value) =>
        // Null and empty are left to [Required], so the two messages do not both appear.
        value is null or "" || (value is string text && IsName(enumType, text));

    public override string FormatErrorMessage(string name) =>
        $"{name} must be one of: {string.Join(", ", Enum.GetNames(enumType))}.";

    private static bool IsName(Type type, string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.GetNames(type).Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
