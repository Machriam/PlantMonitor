namespace PlantMonitorControl.Features.HealthChecking;

public interface IHealthSettingsEditor
{
    DeviceHealth GetHealth();

    DeviceHealth UpdateHealthState(params (HealthState state, bool isActive)[] data);

    void UpdateIrOffset(IrCameraOffset offset);
}

public class HealthSettingsEditor : IHealthSettingsEditor
{
    private const string HealthSettingsFile = "devicehealth.json";
    private static readonly string s_filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), HealthSettingsFile);

    public DeviceHealth GetHealth()
    {
        if (!File.Exists(s_filePath))
        {
            File.WriteAllText(s_filePath, new DeviceHealth()
            {
                DeviceId = Guid.NewGuid().ToString(),
                DeviceName = new PlantList().GetRandomPlant() + $" {Random.Shared.Next(1, 100)}",
                State = HealthState.NA
            }.AsJson());
        }
        return File.ReadAllText(s_filePath).FromJson<DeviceHealth>() ?? throw new Exception("Could not read devicehealth.json");
    }

    public void UpdateIrOffset(IrCameraOffset offset)
    {
        var health = GetHealth();
        health.CameraOffset = offset;
        File.WriteAllText(s_filePath, health.AsJson());
    }

    public DeviceHealth UpdateHealthState(params (HealthState state, bool isActive)[] data)
    {
        var health = GetHealth();
        if (health.State < 0) health.State = HealthState.NA;
        foreach (var (state, isActive) in data)
        {
            if (isActive) health.State |= state;
            else if (health.State.HasFlag(state)) health.State &= ~state;
        }
        File.WriteAllText(s_filePath, health.AsJson());
        return health;
    }
}
