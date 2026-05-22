namespace DumpTether.Domain;

internal static class DomainGuards
{
    public static string NotBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        return value;
    }

    public static string? OptionalTrimmed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string? OptionalHexColor(string? value, string parameterName)
    {
        var normalizedColor = OptionalTrimmed(value);

        if (normalizedColor is null)
        {
            return null;
        }

        if (normalizedColor.Length != 7 ||
            normalizedColor[0] != '#' ||
            normalizedColor.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Color must be a hex color in #RRGGBB format.",
                parameterName);
        }

        return normalizedColor.ToUpperInvariant();
    }
}
