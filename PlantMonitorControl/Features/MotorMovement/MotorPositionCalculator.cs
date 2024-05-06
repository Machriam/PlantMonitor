using System.Collections.Concurrent;

namespace PlantMonitorControl.Features.MotorMovement;

public interface IMotorPositionCalculator
{
    int CurrentPosition();

    void PersistCurrentPosition();

    void UpdatePosition(int stepsMoved);

    void ZeroPosition();

    int StepForTime(long time);

    void ResetHistory();
}

public record struct MotorPositionInfo(int StepCount, long Time);

public class MotorPositionCalculator : IMotorPositionCalculator
{
    private const string CurrentPositionFile = "currentPosition.txt";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPositionFile);
    private static int s_currentPosition;
    private static readonly List<MotorPositionInfo> s_motorPositionHistory = [];
    private static readonly Comparer<MotorPositionInfo> s_positionComparer = Comparer<MotorPositionInfo>.Create((a, b) => a.Time.CompareTo(b.Time));
    private static readonly object s_lock = new();

    public MotorPositionCalculator()
    {
        if (File.Exists(s_filePath)) s_currentPosition = int.Parse(File.ReadAllText(s_filePath));
        else ZeroPosition();
    }

    public void ZeroPosition()
    {
        lock (s_lock) s_currentPosition = 0;
        PersistCurrentPosition();
    }

    public void ResetHistory()
    {
        lock (s_lock)
        {
            s_motorPositionHistory.Clear();
        }
    }

    public void UpdatePosition(int stepsMoved)
    {
        var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        lock (s_lock)
        {
            s_currentPosition += stepsMoved;
            s_motorPositionHistory.Add(new(s_currentPosition, time));
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
        lock (s_lock) File.WriteAllText(s_filePath, s_currentPosition.ToString());
    }

    public int CurrentPosition()
    {
        lock (s_lock) return s_currentPosition;
    }
}
