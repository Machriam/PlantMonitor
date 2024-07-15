using Npgsql;

namespace Plantmonitor.DataModel.DataModel;

public static class NpgsqlDataSourceBuilderExtensions
{
    public static NpgsqlDataSourceBuilder Configure(this NpgsqlDataSourceBuilder builder)
    {
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        builder.MapEnum<PhotoTourEventType>("plantmonitor.photo_tour_event_type");
        return builder;
    }
}
