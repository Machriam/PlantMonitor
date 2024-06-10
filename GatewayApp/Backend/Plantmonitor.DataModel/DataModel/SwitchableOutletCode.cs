using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class SwitchableOutletCode
{
    public long Id { get; set; }

    public string OutletName { get; set; } = null!;

    public long Code { get; set; }

    public bool TurnsOn { get; set; }

    public int ChannelNumber { get; set; }

    public int ChannelBaseNumber { get; set; }

    public string Description { get; set; } = null!;
}
