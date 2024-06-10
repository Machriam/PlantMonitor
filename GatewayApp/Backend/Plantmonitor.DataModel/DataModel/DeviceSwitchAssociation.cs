using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class DeviceSwitchAssociation
{
    public long Id { get; set; }

    public long OutletOnFk { get; set; }

    public long OutletOffFk { get; set; }

    public Guid DeviceId { get; set; }

    public virtual SwitchableOutletCode OutletOffFkNavigation { get; set; } = null!;

    public virtual SwitchableOutletCode OutletOnFkNavigation { get; set; } = null!;
}
