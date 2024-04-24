namespace PlantMonitorControl.Features.MotorMovement;

public interface IMotorPositionCalculator
{
    int CurrentPosition();

    void PersistCurrentPosition();

    void UpdatePosition(int stepsMoved);

    void ZeroPosition();
}

public class MotorPositionCalculator : IMotorPositionCalculator
{
    private const string CurrentPositionFile = "currentPosition.txt";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPositionFile);
    private static int s_currentPosition;
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

    public void UpdatePosition(int stepsMoved)
    {
        lock (s_lock) s_currentPosition += stepsMoved;
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