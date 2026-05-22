using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DumpTether.App.Views;

internal static class SavedViewPayloads
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly string[] ArchiveFilters = ["Active", "Archived", "All"];
    private static readonly string[] FollowUpFilters = ["Any", "Overdue", "Today", "ThisWeek"];
    private static readonly string[] SortFields =
    [
        "lastTouchedAt",
        "createdAt",
        "followUpAt",
        "title",
        "status"
    ];
    private static readonly string[] SortDirections = ["asc", "desc"];

    public static SavedViewFilterRequest DeserializeFilter(string definitionJson)
    {
        return Deserialize<SavedViewFilterRequest>(definitionJson) ??
            NormalizeFilter(null);
    }

    public static SavedViewSortRequest DeserializeSort(string sortJson)
    {
        return Deserialize<SavedViewSortRequest>(sortJson) ??
            NormalizeSort(null);
    }

    public static string SerializeFilter(SavedViewFilterRequest? filter)
    {
        return JsonSerializer.Serialize(NormalizeFilter(filter), JsonSerializerOptions);
    }

    public static string SerializeSort(SavedViewSortRequest? sort)
    {
        return JsonSerializer.Serialize(NormalizeSort(sort), JsonSerializerOptions);
    }

    public static SavedViewFilterRequest NormalizeFilter(SavedViewFilterRequest? filter)
    {
        if (filter is null)
        {
            return new SavedViewFilterRequest(Archive: "Active");
        }

        if (filter.NotViewedSinceDays is < 1)
        {
            throw new ValidationException("NotViewedSinceDays must be greater than zero.");
        }

        if (filter.NotTouchedSinceDays is < 1)
        {
            throw new ValidationException("NotTouchedSinceDays must be greater than zero.");
        }

        return filter with
        {
            Status = NormalizeStatus(filter.Status),
            Category = NormalizeNullableText(filter.Category),
            Color = NormalizeColor(filter.Color),
            Archive = NormalizeOption(
                filter.Archive,
                ArchiveFilters,
                "archive filter",
                defaultValue: "Active"),
            FollowUp = NormalizeOption(
                filter.FollowUp,
                FollowUpFilters,
                "follow-up filter",
                defaultValue: null),
            Text = NormalizeNullableText(filter.Text)
        };
    }

    public static SavedViewSortRequest NormalizeSort(SavedViewSortRequest? sort)
    {
        if (sort is null)
        {
            return new SavedViewSortRequest("lastTouchedAt", "desc");
        }

        return new SavedViewSortRequest(
            NormalizeOption(
                sort.Field,
                SortFields,
                "sort field",
                defaultValue: "lastTouchedAt"),
            NormalizeOption(
                sort.Direction,
                SortDirections,
                "sort direction",
                defaultValue: "desc"));
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? NormalizeStatus(string? status)
    {
        return status is null ? null : status.Trim();
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeColor(string? color)
    {
        var normalizedColor = NormalizeNullableText(color);

        if (normalizedColor is null)
        {
            return null;
        }

        if (normalizedColor.Length != 7 ||
            normalizedColor[0] != '#' ||
            normalizedColor.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ValidationException(
                "Color filter must be a hex color in #RRGGBB format.");
        }

        return normalizedColor.ToUpperInvariant();
    }

    private static string? NormalizeOption(
        string? value,
        IReadOnlyList<string> allowedValues,
        string label,
        string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        var match = allowedValues.FirstOrDefault(allowedValue =>
            string.Equals(allowedValue, value.Trim(), StringComparison.OrdinalIgnoreCase));

        return match ?? throw new ValidationException(
            $"Unsupported {label} '{value}'.");
    }
}
