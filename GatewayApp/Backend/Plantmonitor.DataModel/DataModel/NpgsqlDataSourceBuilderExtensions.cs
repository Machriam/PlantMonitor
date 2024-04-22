using Npgsql;

namespace Plantmonitor.DataModel.DataModel;

public static class NpgsqlDataSourceBuilderExtensions
{
    public static NpgsqlDataSourceBuilder Configure(this NpgsqlDataSourceBuilder builder)
    {
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        return builder;
    }
}