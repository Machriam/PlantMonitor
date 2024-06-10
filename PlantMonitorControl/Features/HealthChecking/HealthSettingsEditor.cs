namespace PlantMonitorControl.Features.HealthChecking;

public interface IHealthSettingsEditor
{
    DeviceHealth GetHealth();

    DeviceHealth UpdateHealthState(params (HealthState state, bool isActive)[] data);
}

public class HealthSettingsEditor : IHealthSettingsEditor
{
    private const string HealthSettingsFile = "devicehealth.json";
    private static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), HealthSettingsFile);

    public DeviceHealth GetHealth()
    {
        if (!File.Exists(_filePath))
        {
            File.WriteAllText(_filePath, new DeviceHealth()
            {
                DeviceId = Guid.NewGuid().ToString(),
                DeviceName = new PlantList().GetRandomPlant() + $" {Random.Shared.Next(1, 100)}",
                State = HealthState.NA
            }.AsJson());
        }
        return File.ReadAllText(_filePath).FromJson<DeviceHealth>() ?? throw new Exception("Could not read devicehealth.json");
    }

    public DeviceHealth UpdateHealthState(params (HealthState state, bool isActive)[] data)
    {
        var health = GetHealth();
        foreach (var (state, isActive) in data)
        {
            health.State |= (isActive ? state : ~state);
        }
        File.WriteAllText(_filePath, health.AsJson());
        return health;
    }
}
