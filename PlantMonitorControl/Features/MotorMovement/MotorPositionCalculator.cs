using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MotorMovement;

public interface IMotorPositionCalculator
{
    void PersistCurrentPosition();

    void UpdatePosition(int stepsMoved, int maxAllowedPosition, int minAllowedPosition);

    void ZeroPosition();

    int StepForTime(long time);

    void ResetHistory();

    void ToggleMotorEngage(bool shouldEngage);

    void MoveMotor(int steps, int minTime, int maxTime, int rampLength, int maxAllowedPosition, int minAllowedPosition);

    MotorPosition CurrentPosition();
}

public record struct MotorPositionInfo(int StepCount, long Time);

public class MotorPositionCalculator : IMotorPositionCalculator
{
    private const string CurrentPositionFile = "currentPosition.txt";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPositionFile);
    private static int s_currentPosition;
    private static readonly List<MotorPositionInfo> s_motorPositionHistory = [];
    private static readonly Comparer<MotorPositionInfo> s_positionComparer = Comparer<MotorPositionInfo>.Create((a, b) => a.Time.CompareTo(b.Time));
    private static readonly object s_positionLock = new();
    private static readonly object s_engageLock = new();
    private static bool s_isEngaged = true;
    private readonly IEnvironmentConfiguration _configuration;
    private readonly IGpioInteropFactory _gpioFactory;

    public MotorPositionCalculator(IEnvironmentConfiguration configuration, IGpioInteropFactory gpioFactory)
    {
        if (File.Exists(s_filePath)) s_currentPosition = int.Parse(File.ReadAllText(s_filePath));
        else ZeroPosition();
        _configuration = configuration;
        _gpioFactory = gpioFactory;
    }

    public void ToggleMotorEngage(bool shouldEngage)
    {
        var locked = PinValue.Low;
        var released = PinValue.High;
        using var controller = _gpioFactory.Create();
        var pinout = _configuration.MotorPinout;
        controller.OpenPin(pinout.Enable, PinMode.Output);
        controller.Write(pinout.Enable, shouldEngage ? locked : released);
        lock (s_engageLock) s_isEngaged = shouldEngage;
    }

    public void MoveMotor(int steps, int minTime, int maxTime, int rampLength, int maxAllowedPosition, int minAllowedPosition)
    {
        var sw = new Stopwatch();
        var microSecondsPerTick = 1000d * 1000d / Stopwatch.Frequency;
        using var controller = _gpioFactory.Create();
        var pinout = _configuration.MotorPinout;
        controller.OpenPin(pinout.Direction, PinMode.Output);
        controller.OpenPin(pinout.Pulse, PinMode.Output);
        var left = PinValue.High;
        var right = PinValue.Low;

        controller.Write(pinout.Direction, steps < 0 ? left : right);
        var stepUnit = steps < 0 ? -1 : 1;
        var stepsToMove = Math.Abs(steps);
        var rampFunction = stepsToMove.CreateLogisticRampFunction(minTime, maxTime, rampLength);
        for (var i = 0; i < stepsToMove; i++)
        {
            controller.Write(pinout.Pulse, PinValue.High);
            sw.Restart();
            var delay = (int)(rampFunction(i) * 0.5f);
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
            controller.Write(pinout.Pulse, PinValue.Low);
            sw.Restart();
            UpdatePosition(stepUnit, maxAllowedPosition, minAllowedPosition);
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
        }
        PersistCurrentPosition();
    }

    public void ZeroPosition()
    {
        lock (s_positionLock) s_currentPosition = 0;
        PersistCurrentPosition();
    }

    public void ResetHistory()
    {
        lock (s_positionLock)
        {
            s_motorPositionHistory.Clear();
            var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            s_motorPositionHistory.Add(new(s_currentPosition, time));
        }
    }

    public void UpdatePosition(int stepsMoved, int maxAllowedPosition, int minAllowedPosition)
    {
        var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        lock (s_positionLock)
        {
            lock (s_engageLock)
            {
                if ((s_currentPosition > maxAllowedPosition || s_currentPosition < minAllowedPosition) && s_isEngaged) ToggleMotorEngage(false);
                if (!s_isEngaged) return;
                s_currentPosition += stepsMoved;
                s_motorPositionHistory.Add(new(s_currentPosition, time));
            }
        }
    }

    public int StepForTime(long time)
    {
        if (s_motorPositionHistory.Count == 0) return int.MinValue;
        var index = s_motorPositionHistory.BinarySearch(new(0, time), s_positionComparer);
        if (index == -1) return s_motorPositionHistory[0].StepCount;
        return index >= 0 ? s_motorPositionHistory[index].StepCount : s_motorPositionHistory[(~index) - 1].StepCount;
    }

    public void PersistCurrentPosition()
    {
        lock (s_positionLock) File.WriteAllText(s_filePath, s_currentPosition.ToString());
    }

    public MotorPosition CurrentPosition()
    {
        lock (s_positionLock) lock (s_engageLock) return new(s_isEngaged, s_currentPosition);
    }
}
