using Npgsql.Internal;
using System;
using System.Collections.Generic;

namespace Plantmonitor.DataModel.DataModel;

public record struct MovementPoint(int StepOffset, float FocusInCentimeter, int Speed, string Comment);

public class MovementPlan
{
    public List<MovementPoint> StepPoints { get; set; } = [];
}

public partial class DeviceMovement
{
    public MovementPlan MovementPlan { get; set; } = new();
}