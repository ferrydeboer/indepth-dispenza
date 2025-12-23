using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InDepthDispenza.Functions.Interfaces;

/// <summary>
/// Value type representing a taxonomy version in the form major.minor.
/// Accepts optional leading 'v' on input. Provides comparison operators and implicit conversion from string.
/// Serializes to a string with leading 'v' (e.g., "v1.0").
/// </summary>
[DebuggerDisplay("{ToString()}")]
public readonly struct TaxonomyVersion : IComparable<TaxonomyVersion>, IEquatable<TaxonomyVersion>
{
    public int Major { get; }
    public int Minor { get; }

    public TaxonomyVersion(int major, int minor)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
        Major = major;
        Minor = minor;
    }

    public static bool TryParse(string? value, out TaxonomyVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Trim and remove starting 'v' or 'V'
        var s = value.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);

        var parts = s.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)) return false;
        if (major < 0 || minor < 0) return false;

        version = new TaxonomyVersion(major, minor);
        return true;
    }

    public static TaxonomyVersion Parse(string value)
    {
        if (TryParse(value, out var v)) return v;
        throw new FormatException($"Invalid taxonomy version '{value}'. Expected format 'major.minor' optionally prefixed with 'v'.");
    }

    public override string ToString() => $"v{Major}.{Minor}";

    public int CompareTo(TaxonomyVersion other)
    {
        var c = Major.CompareTo(other.Major);
        if (c != 0) return c;
        return Minor.CompareTo(other.Minor);
    }

    public bool Equals(TaxonomyVersion other) => Major == other.Major && Minor == other.Minor;

    public override bool Equals(object? obj) => obj is TaxonomyVersion tv && Equals(tv);

    public override int GetHashCode() => HashCode.Combine(Major, Minor);

    public static bool operator >(TaxonomyVersion a, TaxonomyVersion b) => a.CompareTo(b) > 0;
    public static bool operator <(TaxonomyVersion a, TaxonomyVersion b) => a.CompareTo(b) < 0;
    public static bool operator >=(TaxonomyVersion a, TaxonomyVersion b) => a.CompareTo(b) >= 0;
    public static bool operator <=(TaxonomyVersion a, TaxonomyVersion b) => a.CompareTo(b) <= 0;
    public static bool operator ==(TaxonomyVersion a, TaxonomyVersion b) => a.Equals(b);
    public static bool operator !=(TaxonomyVersion a, TaxonomyVersion b) => !a.Equals(b);

    // Backwards compatibility: allow implicit construction from string
    public static implicit operator TaxonomyVersion(string s) => Parse(s);
    // And implicit conversion to string when needed
    public static implicit operator string(TaxonomyVersion v) => v.ToString();

    /// <summary>
    /// Returns a new version with the same Major and Minor incremented by 1.
    /// </summary>
    public TaxonomyVersion IncrementMinor() => new TaxonomyVersion(Major, checked(Minor + 1));
}

/// <summary>
/// System.Text.Json converter for TaxonomyVersion.
/// </summary>
public sealed class SystemTextJsonTaxonomyVersionConverter : JsonConverter<TaxonomyVersion>
{
    public override TaxonomyVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            return TaxonomyVersion.Parse(s ?? string.Empty);
        }
        throw new JsonException("Expected string for TaxonomyVersion");
    }

    public override void Write(Utf8JsonWriter writer, TaxonomyVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Newtonsoft.Json converter for TaxonomyVersion.
/// </summary>
public sealed class NewtonsoftTaxonomyVersionConverter : global::Newtonsoft.Json.JsonConverter<TaxonomyVersion>
{
    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, TaxonomyVersion value, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }

    public override TaxonomyVersion ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, TaxonomyVersion existingValue, bool hasExistingValue, global::Newtonsoft.Json.JsonSerializer serializer)
    {
        if (reader.TokenType == Newtonsoft.Json.JsonToken.String)
        {
            var s = (string?)reader.Value;
            return TaxonomyVersion.Parse(s ?? string.Empty);
        }
        throw new Newtonsoft.Json.JsonSerializationException("Expected string for TaxonomyVersion");
    }
}
