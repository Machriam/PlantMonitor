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
    public static void SetValue(this IQueryable<ConfigurationDatum> data, ConfigurationDatumKeys key, string json)
    {
        var keyName = Enum.GetName(key) ?? throw new Exception("Enum key cannot be empty");
        var existingValue = data.FirstOrDefault(d => d.Key == keyName);
        if (existingValue == default)
        {
            data.Add(new ConfigurationDatum { Key = keyName, Value = json });
        }
        else
        {
            existingValue.Value = json;
        }
    }

    public static string GetValue(this IQueryable<ConfigurationDatum> data, ConfigurationDatumKeys key)
    {
        return data.FirstOrDefault(d => d.Key == Enum.GetName(key))?.Value ?? "";
    }
}

public partial class ConfigurationDatum
{
}
