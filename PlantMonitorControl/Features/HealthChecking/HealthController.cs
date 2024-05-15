using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Shared.Features.HealthChecking;
using PlantMonitorControl.Features.ImageTaking;

namespace PlantMonitorControl.Features.HealthChecking;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IHealthSettingsEditor healthSettings) : ControllerBase
{
    [HttpGet()]
    public async Task<DeviceHealth> GetDeviceHealth([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop visCamera)
    {
        var cameraFunctional = await visCamera.CameraFunctional();
        var cameraFound = await visCamera.CameraFound();
        var health = healthSettings.GetHealth();
        if (health.State.HasFlag(HealthState.NoirCameraFound) != cameraFound || health.State.HasFlag(HealthState.NoirCameraFunctional) != cameraFunctional)
            return healthSettings.UpdateHealthState((HealthState.NoirCameraFound, cameraFound), (HealthState.NoirCameraFunctional, cameraFunctional));
        return health;
    }
}