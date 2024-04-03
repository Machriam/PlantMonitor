using Plantmonitor.Shared.Extensions;
using Plantmonitor.Shared.Features.HealthChecking;

namespace PlantMonitorControl.Features.HealthChecking;

public interface IHealthSettingsEditor
{
    DeviceHealth GetHealth();

    void WriteHealthState(HealthState state);
}

public class HealthSettingsEditor : IHealthSettingsEditor
{
    private const string HealthSettingsFile = "~/devicehealth.json";

    public DeviceHealth GetHealth()
    {
        if (!File.Exists(HealthSettingsFile))
        {
            File.WriteAllText(HealthSettingsFile, new DeviceHealth()
            {
                DeviceId = Guid.NewGuid().ToString(),
                DeviceName = new PlantList().GetRandomPlant() + $" {Random.Shared.Next(1, 100)}",
                State = HealthState.NA
            }.AsJson());
        }
        return File.ReadAllText(HealthSettingsFile).FromJson<DeviceHealth>() ?? throw new Exception("Could not read devicehealth.json");
    }

    public void WriteHealthState(HealthState state)
    {
        var health = GetHealth();
        health.State = state;
        File.WriteAllText(HealthSettingsFile, health.AsJson());
    }
}