namespace PlantMonitorControl.Features.MotorMovement;

public static class RampFunctionExtensions
{
    public static Func<int, int> CreateRampFunction(this int stepCount, int minTime, int maxTime, int maxRampLength = 50)
    {
        var rampLength = (float)maxRampLength;
        if (stepCount < maxRampLength * 2)
        {
            rampLength = float.Ceiling(stepCount / 2f);
        }
        var fallingRampStart = stepCount - rampLength;
        var timeIncrement = (maxTime - minTime) / (float)maxRampLength;
        var plateauTime = maxTime - (timeIncrement * rampLength);
        return x =>
        {
            if (x >= fallingRampStart)
            {
                return (int)(plateauTime + ((x - fallingRampStart + 1) * timeIncrement));
            }
            return (int)(maxTime - (Math.Min(x, rampLength) * timeIncrement));
        };
    }
}