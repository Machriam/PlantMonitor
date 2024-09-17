using System.Text.Json.Serialization;
using System.Text.Json;
using Npgsql;
using System.Globalization;

namespace Plantmonitor.DataModel.DataModel;

public class DateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text == null) return DateTime.MinValue;
        return DateTime.Parse(text);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

public static class NpgsqlDataSourceBuilderExtensions
{
    public static NpgsqlDataSourceBuilder Configure(this NpgsqlDataSourceBuilder builder)
    {
        builder.EnableDynamicJson();
        var options = new JsonSerializerOptions();
        options.Converters.Add(new DateTimeConverter());
        builder.ConfigureJsonOptions(options);
        builder.EnableUnmappedTypes();
        builder.MapEnum<PhotoTourEventType>("plantmonitor.photo_tour_event_type");
        return builder;
    }
}
