using Npgsql.Internal;
using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public class MovementPoint()
{
    public int StepOffset { get; set; }
    public float FocusInCentimeter { get; set; }
    public int Speed { get; set; }
    public string Comment { get; set; } = "";
}

public class MovementPlan
{
    public List<MovementPoint> StepPoints { get; set; } = [];
}

public partial class DeviceMovement
{
    public MovementPlan MovementPlan { get; set; } = new();
}