using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public enum ConfigurationDatumKeys
{
    PatchNumber
}

public partial class ConfigurationDatum
{
    public const string PatchNumber = nameof(PatchNumber);

    public bool FindEntry(ConfigurationDatumKeys key)
    {
        return Key == Enum.GetName(key);
    }
}