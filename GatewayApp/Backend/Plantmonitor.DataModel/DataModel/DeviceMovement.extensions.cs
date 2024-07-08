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

    public IEnumerable<int> PositionsOfMovementPlan()
    {
        var currentPosition = 0;
        var result = new List<int>();
        foreach (var position in MovementPlan.StepPoints)
        {
            currentPosition += position.StepOffset;
            result.Add(currentPosition);
        }
        return result;
    }

    public (int MaxStop, int MinStop) GetSafetyStops()
    {
        var positions = PositionsOfMovementPlan();
        return ((int)(positions.Max() * 1.05f), (int)(positions.Min() * 1.05f));
    }
}
