using System.Text.Json.Serialization;
using System.Text.Json;

namespace Plantmonitor.Shared.Extensions;

public static class JsonExtensions
{
    private static readonly Dictionary<string, JsonSerializerOptions> _readOptionsCache = [];
    private static readonly Dictionary<string, JsonSerializerOptions> _writeOptionsCache = [];

    private static JsonSerializerOptions GetWriteOptions(bool includeFields = true, bool ignoreCycles = true, bool writeIndented = false, bool writeInfinity = false)
    {
        var key = new[] { includeFields, ignoreCycles, writeIndented, writeInfinity }.Concat(",");
        if (_writeOptionsCache.TryGetValue(key, out var options)) return options;
        _writeOptionsCache.Add(key, new JsonSerializerOptions()
        {
            IncludeFields = includeFields,
            NumberHandling = writeInfinity ? JsonNumberHandling.AllowNamedFloatingPointLiterals : JsonNumberHandling.Strict,
            ReferenceHandler = ignoreCycles ? ReferenceHandler.IgnoreCycles : ReferenceHandler.Preserve,
            WriteIndented = writeIndented
        });
        return _writeOptionsCache[key];
    }

    private static JsonSerializerOptions GetReadOptions(bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false)
    {
        var key = new[] { includeFields, ignoreCycles, allowNumberReadingFromString }.Concat(",");
        if (_readOptionsCache.TryGetValue(key, out var options)) return options;
        _readOptionsCache.Add(key, new JsonSerializerOptions()
        {
            IncludeFields = includeFields,
            PropertyNameCaseInsensitive = true,
            NumberHandling = allowNumberReadingFromString ? JsonNumberHandling.AllowReadingFromString : JsonNumberHandling.Strict,
            ReferenceHandler = ignoreCycles ? ReferenceHandler.IgnoreCycles : ReferenceHandler.Preserve
        });
        return _readOptionsCache[key];
    }

    public static T? FromJson<T>(this Stream utf8Stream, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : new()
    {
        return JsonSerializer.Deserialize<T>(utf8Stream, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
    }

    public static T? FromJson<T>(this byte[] utf8Json, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : new()
    {
        return JsonSerializer.Deserialize<T>(utf8Json, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
    }

    public static T? FromJson<T>(this string json, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : new()
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static string AsJson<T>(this T obj, bool includeFields = true, bool ignoreCycles = true, bool writeIndented = false, bool writeInfinity = false)
    {
        return JsonSerializer.Serialize(obj, GetWriteOptions(includeFields, ignoreCycles, writeIndented, writeInfinity));
    }
}
