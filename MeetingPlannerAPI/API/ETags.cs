using System.Globalization;

namespace MeetingPlannerAPI.API;

/// <summary>Maps a meeting's <c>Version</c> to and from a strong HTTP entity tag such as <c>"3"</c>.</summary>
internal static class ETags
{
    public static string Format(int version) => $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Accepts exactly one strong tag; <c>*</c>, weak tags, lists and malformed values are rejected.</summary>
    public static bool TryParse(string? header, out int version)
    {
        version = 0;
        if (header is null)
            return false;

        ReadOnlySpan<char> value = header.AsSpan().Trim();
        return value.Length > 2
            && value[0] == '"'
            && value[^1] == '"'
            && int.TryParse(value[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out version)
            && version > 0;
    }
}
