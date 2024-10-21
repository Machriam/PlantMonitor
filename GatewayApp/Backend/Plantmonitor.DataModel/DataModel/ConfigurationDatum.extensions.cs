using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public enum ConfigurationDatumKeys
{
    PatchNumber,
    AllSeenDevices
}

public static class ConfigurationDatumExtensions
{
    public static string GetValue(this IQueryable<ConfigurationDatum> data, ConfigurationDatumKeys key)
    {
        return data.FirstOrDefault(d => d.Key == Enum.GetName(key))?.Value ?? "";
    }
}

public partial class ConfigurationDatum
{
}
