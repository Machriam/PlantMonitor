﻿using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.ImageTaking;
using PlantMonitorControl.Features.MeasureTemperature;
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
        [FromServices] IOutletSwitcher switcher, [FromServices] IClick2TempInterop tempInterop)
    {
        var cameraFunctional = await visCamera.CameraFunctional();
        var cameraFound = await visCamera.CameraFound();
        var irFunctional = await irCamera.CameraFunctional();
        var irFound = await irCamera.CameraFound();
        var canSwitchOutlets = await switcher.DeviceCanSwitchOutlets();
        var tempDevices = tempInterop.GetDevices();
        return healthSettings.UpdateHealthState(
            (HealthState.NoirCameraFound, cameraFound),
            (HealthState.NoirCameraFunctional, cameraFunctional),
            (HealthState.ThermalCameraFunctional, irFunctional),
            (HealthState.HasTemperatureSensor, tempDevices.Any()),
            (HealthState.ThermalCameraFound, irFound),
            (HealthState.CanSwitchOutlets, canSwitchOutlets));
    }

    [HttpPost("updateiroffset")]
    public void UpdateIrOffset([FromBody] IrCameraOffset newOffset)
    {
        healthSettings.UpdateIrOffset(newOffset);
    }

    [HttpPost("rebootdevice")]
    public void RebootDevice()
    {
        var process = new ProcessStartInfo("reboot", "now");
        new Process() { StartInfo = process }.Start();
    }

    [HttpGet("logs")]
    public async Task<string> GetLogs()
    {
        var lastLog = Directory.EnumerateFiles(Path.GetDirectoryName(ConfigurationOptions.LogFileLocation) ?? "", "server*.logs")
            .Select(f => (WriteTime: System.IO.File.GetLastWriteTimeUtc(f), File: f))
            .OrderByDescending(f => f.WriteTime)
            .FirstOrDefault();
        if (lastLog.File.IsEmpty()) return "";
        await using var file = System.IO.File.Open(lastLog.File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }

    [HttpGet("alllogs")]
    public async Task<string> GetAllLogs()
    {
        var result = new StringBuilder();
        var logs = Directory.EnumerateFiles(Path.GetDirectoryName(ConfigurationOptions.LogFileLocation) ?? "", "server*.logs")
            .Select(f => (WriteTime: System.IO.File.GetLastWriteTimeUtc(f), File: f))
            .OrderBy(f => f.WriteTime);
        foreach (var log in logs)
        {
            await using var file = System.IO.File.Open(log.File, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            result.AppendLine(reader.ReadToEnd());
        }
        return result.ToString();
    }
}
