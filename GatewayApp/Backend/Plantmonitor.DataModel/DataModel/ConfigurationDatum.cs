using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class ConfigurationDatum
{
    public long Id { get; set; }

    public string Key { get; set; } = null!;

    public string Value { get; set; } = null!;
}
