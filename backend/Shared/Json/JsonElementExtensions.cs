using System.Text.Json;
using System.Text.RegularExpressions;

namespace NextStep.Shared.Json;

/// <summary>Tolerant readers for loosely-typed JSON (AI agent payloads).</summary>
public static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    public static int? GetIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number)
            return null;
            
        // Use TryGetInt32 first, fallback to GetDouble and cast if it's a float
        if (v.TryGetInt32(out var i)) return i;
        return (int)v.GetDouble();
    }

    public static int? GetStringAsIntOrDefault(this JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number) return v.GetInt32();
        if (v.ValueKind == JsonValueKind.String)
        {
            var str = v.GetString();
            if (string.IsNullOrEmpty(str)) return null;
            var match = Regex.Match(str, @"\d+");
            if (match.Success && int.TryParse(match.Value, out var parsed))
                return parsed;
        }
        return null;
    }

    public static List<string> GetStringList(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? [.. arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)]
            : [];

    public static double? GetDoubleOrDefault(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    public static JsonElement? GetPropertyOrNull(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v : null;
}
