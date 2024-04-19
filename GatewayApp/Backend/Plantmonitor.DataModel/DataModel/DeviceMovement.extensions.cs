using Npgsql.Internal;
using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public class MovementPlan
{
    public List<int> StepPoints { get; set; } = [];
    public int Speed { get; set; }
}

public partial class DeviceMovement
{
    public MovementPlan MovementPlan { get; set; } = new();
}