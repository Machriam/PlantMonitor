namespace PlantMonitorControl.Features.MotorMovement;

public static class RampFunctionExtensions
{
    private static Dictionary<int, int> BuildLogisticCurve(int minTime, int maxTime, int rampSize)
    {
        const int sampleLength = 100;
        var L = maxTime - minTime;
        const int x0 = 50;
        const float k = 0.12f;
        int LogisticFunction(float x) => (int)(maxTime - (L / (1f + MathF.Exp(-k * (x - x0)))));
        var result = Enumerable.Range(1, sampleLength).ToDictionary(x => x - 1, x => LogisticFunction(x));
        result.Add(sampleLength, result[sampleLength - 1]);
        if (rampSize < sampleLength)
        {
            var stepSize = sampleLength / rampSize;
            var i = 0;
            return Enumerable.Range(0, rampSize)
                .Select(x => x * stepSize)
                .ToDictionary(_ => i++, x => result[x]);
        }
        else if (rampSize > sampleLength)
        {
            var stepSize = sampleLength / (float)rampSize;
            return Enumerable.Range(0, rampSize)
                .Select(x =>
                {
                    var first = (int)(x * stepSize);
                    var second = (int)(x * stepSize) + 1;
                    var fractionToNext = first - (x * stepSize);
                    return (X: x, Value: result[first] - (result[second] - result[first]) * fractionToNext);
                })
                .ToDictionary(x => x.X, x => (int)x.Value);
        }
        return result;
    }

    public static Func<int, int> CreateLogisticRampFunction(this int stepCount, int minTime, int maxTime, int maxRampLength = 50)
    {
        var rampLength = (float)maxRampLength;
        if (stepCount < maxRampLength * 2)
        {
            rampLength = float.Ceiling(stepCount / 2f);
        }
        var rampValueByIndex = BuildLogisticCurve(minTime, maxTime, maxRampLength);
        var fallingRampStart = stepCount - rampLength;
        return x =>
        {
            if (x < rampLength) return rampValueByIndex[x];
            if (x > fallingRampStart) return rampValueByIndex[(int)(rampLength + fallingRampStart - x)];
            return rampValueByIndex[(int)rampLength - 1];
        };
    }
}