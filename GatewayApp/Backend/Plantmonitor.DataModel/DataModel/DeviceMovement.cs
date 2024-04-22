using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public partial class DeviceMovement
{
    public long Id { get; set; }

    public Guid DeviceId { get; set; }

    public string MovementPlanJson { get; set; } = null!;

    public string Name { get; set; } = null!;
}
