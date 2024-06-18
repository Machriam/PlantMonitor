using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.ImageTaking;
using PlantMonitorControl.Features.SwitchOutlets;

namespace PlantMonitorControl.Features.HealthChecking;

[ApiController]
[Route("api/[controller]")]
public class HealthController(IHealthSettingsEditor healthSettings) : ControllerBase
{
    [HttpGet("lastdevicehealth")]
    public DeviceHealth LastDeviceHealth()
    {
        return healthSettings.GetHealth();
    }

    [HttpGet("checkdevicehealth")]
    public async Task<DeviceHealth> CheckDeviceHealth([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop visCamera,
        [FromKeyedServices(ICameraInterop.IrCamera)] ICameraInterop irCamera,
        [FromServices] IOutletSwitcher switcher)
    {
        var cameraFunctional = await visCamera.CameraFunctional();
        var cameraFound = await visCamera.CameraFound();
        var irFunctional = await irCamera.CameraFunctional();
        var irFound = await irCamera.CameraFound();
        var canSwitchOutlets = await switcher.DeviceCanSwitchOutlets();
        return healthSettings.UpdateHealthState(
            (HealthState.NoirCameraFound, cameraFound),
            (HealthState.NoirCameraFunctional, cameraFunctional),
            (HealthState.ThermalCameraFunctional, irFunctional),
            (HealthState.ThermalCameraFound, irFound),
            (HealthState.CanSwitchOutlets, canSwitchOutlets));
    }

    [HttpPost("updateiroffset")]
    public void UpdateIrOffset([FromBody] IrCameraOffset newOffset)
    {
        healthSettings.UpdateIrOffset(newOffset);
    }

    [HttpGet("logs")]
    public async Task<string> GetLogs()
    {
        await using var file = System.IO.File.Open(ConfigurationOptions.LogFileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }
}
