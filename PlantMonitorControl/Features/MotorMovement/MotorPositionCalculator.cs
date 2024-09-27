using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.ImageTaking;
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

    Task MoveMotor(int steps, int minTime, int maxTime, int rampLength, int maxAllowedPosition, int minAllowedPosition);

    MotorPosition CurrentPosition();
}

public record struct MotorPositionInfo(int StepCount, long Time);

public class MotorPositionCalculator : IMotorPositionCalculator
{
    private const string CurrentPositionFile = "currentPosition.txt";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPositionFile);
    private static int s_currentPosition;
    private static bool s_dirtyPosition;
    private static readonly List<MotorPositionInfo> s_motorPositionHistory = [];
    private static readonly Comparer<MotorPositionInfo> s_positionComparer = Comparer<MotorPositionInfo>.Create((a, b) => a.Time.CompareTo(b.Time));
    private static readonly object s_positionLock = new();
    private static readonly object s_engageLock = new();
    private static readonly object s_dirtyLock = new();
    private static bool s_isEngaged = true;
    private const string DirtyPositionSymbol = "?";
    private readonly IEnvironmentConfiguration _configuration;
    private readonly IGpioInteropFactory _gpioFactory;
    private readonly ILogger<MotorPositionCalculator> _logger;

    public MotorPositionCalculator(IEnvironmentConfiguration configuration, IGpioInteropFactory gpioFactory, ILogger<MotorPositionCalculator> logger)
    {
        if (File.Exists(s_filePath))
        {
            var positionText = File.ReadAllText(s_filePath);
            logger.LogInformation("Read position from file: {position}", positionText);
            lock (s_dirtyLock) s_dirtyPosition = false;
            if (positionText.Contains(DirtyPositionSymbol))
            {
                lock (s_dirtyLock) s_dirtyPosition = true;
                positionText = positionText.Replace(DirtyPositionSymbol, "");
            }
            lock (s_positionLock) s_currentPosition = int.Parse(positionText);
        }
        else
        {
            ZeroPosition();
            lock (s_dirtyLock) s_dirtyPosition = true;
        }
        logger.LogInformation("Initialized Motor with: Pos: {position}, Dirty: {dirty}, Engaged: {engaged}", s_currentPosition, s_dirtyPosition, s_isEngaged);
        _configuration = configuration;
        _gpioFactory = gpioFactory;
        _logger = logger;
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

    public async Task MoveMotor(int steps, int minTime, int maxTime, int rampLength, int maxAllowedPosition, int minAllowedPosition)
    {
        lock (s_dirtyLock)
        {
            if (s_dirtyPosition) throw new Exception("Position is dirty. Zeroing must be done or waited until the current movement completes");
            s_dirtyPosition = true;
        }
        PersistCurrentPosition();
        var pinout = _configuration.MotorPinout;
        var left = 1;
        var right = 0;
        var direction = steps < 0 ? left : right;
        var stepUnit = steps < 0 ? -1 : 1;
        var stepsToMove = Math.Abs(steps);

        var rampFunction = stepsToMove.CreateLogisticRampFunction(minTime, maxTime, rampLength);
        var motorMoveDelays = EstimatePositionUpdates(maxAllowedPosition, minAllowedPosition, stepsToMove, rampFunction);
        await new Process().RunProcess(_configuration.MotorMovementPrograms.MoveMotor,
            MotorMovementPrograms.ConstructArgumentList(pinout.GpioPinNumberDirection, pinout.GpioPinNumberPulse,
            direction, s_filePath, stepUnit, maxAllowedPosition, minAllowedPosition, motorMoveDelays));
        lock (s_dirtyLock) s_dirtyPosition = false;
        PersistCurrentPosition();
    }

    private List<int> EstimatePositionUpdates(int maxAllowedPosition, int minAllowedPosition, int stepsToMove, Func<int, int> rampFunction)
    {
        var currentDelay = 0;
        var currentPosition = 0;
        var positionUpdate = new List<(int Delay, int PositionIncrease)>();
        var moveDelays = new List<int>();
        for (var i = 0; i < stepsToMove; i++)
        {
            var delay = rampFunction(i);
            moveDelays.Add(delay);
            currentDelay += delay;
            currentPosition++;
            if (currentDelay > 10_000 && i < stepsToMove - 1)
            {
                positionUpdate.Add((currentDelay, currentPosition));
                currentDelay = 0;
                currentPosition = 0;
            }
        }
        async Task UpdatePositionTask()
        {
            foreach (var update in positionUpdate)
            {
                await Task.Delay(update.Delay);
                UpdatePosition(update.PositionIncrease, maxAllowedPosition, minAllowedPosition);
            }
        }
        UpdatePositionTask().RunInBackground(ex => ex.LogError());
        return moveDelays;
    }

    public void ZeroPosition()
    {
        _logger.LogInformation("Zeroing position");
        lock (s_positionLock) lock (s_dirtyLock)
            {
                s_currentPosition = 0;
                s_dirtyPosition = false;
            }
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
        lock (s_positionLock) lock (s_dirtyLock)
            {
                var positionText = s_currentPosition.ToString() + (s_dirtyPosition ? DirtyPositionSymbol : "");
                File.WriteAllText(s_filePath, positionText);
                _logger.LogInformation("Persisting position {position}", positionText);
            }
    }

    public MotorPosition CurrentPosition()
    {
        lock (s_positionLock) lock (s_engageLock) lock (s_dirtyLock) return new(s_isEngaged, s_currentPosition, s_dirtyPosition);
    }
}
