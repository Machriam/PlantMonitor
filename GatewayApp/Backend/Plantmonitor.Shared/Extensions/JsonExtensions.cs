using System.Text.Json.Serialization;
using System.Text.Json;

namespace Plantmonitor.Shared.Extensions;

public static class JsonExtensions
{
    private static readonly Dictionary<(bool, bool, bool), JsonSerializerOptions> _readOptionsCache = [];
    private static readonly Dictionary<(bool, bool), JsonSerializerOptions> _writeOptionsCache = [];

    private static JsonSerializerOptions GetWriteOptions(bool includeFields = true, bool ignoreCycles = true)
    {
        var key = (includeFields, ignoreCycles);
        if (_writeOptionsCache.TryGetValue(key, out var options)) return options;
        _writeOptionsCache.Add(key, new JsonSerializerOptions()
        {
            IncludeFields = includeFields,
            ReferenceHandler = ignoreCycles ? ReferenceHandler.IgnoreCycles : ReferenceHandler.Preserve
        });
        return _writeOptionsCache[key];
    }

    private static JsonSerializerOptions GetReadOptions(bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false)
    {
        var key = (includeFields, ignoreCycles, allowNumberReadingFromString);
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

    public static T? FromJson<T>(this Stream utf8Stream, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : class
    {
        return JsonSerializer.Deserialize<T>(utf8Stream, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
    }

    public static T? FromJson<T>(this byte[] utf8Json, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : class
    {
        return JsonSerializer.Deserialize<T>(utf8Json, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
    }

    public static T? FromJson<T>(this string json, bool includeFields = true, bool ignoreCycles = true, bool allowNumberReadingFromString = false) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, GetReadOptions(includeFields, ignoreCycles, allowNumberReadingFromString));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string AsJson<T>(this T obj, bool includeFields = true, bool ignoreCycles = true)
    {
        return JsonSerializer.Serialize(obj, GetWriteOptions(includeFields, ignoreCycles));
    }
}