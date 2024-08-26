using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

[ApiController]
[Route("api/[controller]")]
public class AutomaticPhotoTourController(IDataContext context, IDeviceConnectionEventBus eventBus, IDeviceApiFactory deviceFactory)
{
    public record struct TemperatureMeasurementInfo(string Guid, string Comment);
    public record struct AutomaticTourStartInfo(float IntervallInMinutes, long MovementPlan, TemperatureMeasurementInfo[] TemperatureMeasureDevice,
        string Comment, string Name, string DeviceGuid, bool ShouldUseIR, float PixelSizeInMm);
    public record struct PhotoTourInfo(string Name, bool Finished, long Id, DateTime FirstEvent, DateTime LastEvent, float IntervallInMinutes, string Comment, float PixelSizeInMm);

    [HttpPost("updatephototour")]
    public void UpdatePhotoTour(long id, float newIntervallInMinutes, float pixelSizeInMm)
    {
        var tour = context.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .First(pt => pt.Id == id);
        tour.IntervallInMinutes = newIntervallInMinutes;
        tour.PixelSizeInMm = pixelSizeInMm;
        context.SaveChanges();
    }

    [HttpPost("pausephototour")]
    public async Task PausePhotoTour(long id, bool shouldBePaused)
    {
        var tour = context.AutomaticPhotoTours
            .Include(apt => apt.TemperatureMeasurements)
            .First(pt => pt.Id == id);
        var movementPlan = context.DeviceMovements.First(dm => dm.DeviceId == tour.DeviceId);
        var associatedTemperatureDevices = tour.TemperatureMeasurements
            .ToList()
            .Where(tm => !tm.IsThermalCamera())
            .Select(tm => new TemperatureMeasurementInfo(tm.DeviceId.ToString(), tm.Comment))
            .ToList();
        if (!shouldBePaused)
        {
            _ = await CheckStartConditions(context, deviceFactory, associatedTemperatureDevices, tour.DeviceId.ToString(), movementPlan.Id, eventBus);
        }
        tour.Finished = shouldBePaused;
        foreach (var measurement in tour.TemperatureMeasurements) measurement.Finished = shouldBePaused;
        context.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            PhotoTourFk = id,
            Timestamp = DateTime.UtcNow,
            Type = PhotoTourEventType.Information,
            Message = $"Photo tour {(shouldBePaused ? "stopped" : "resumed")}",
        });
        context.SaveChanges();
    }

    [HttpGet("events")]
    public IEnumerable<PhotoTourEvent> GetEvents(long photoTourId, bool allLogs)
    {
        var result = context.PhotoTourEvents
            .OrderByDescending(pte => pte.Timestamp)
            .Where(pte => pte.PhotoTourFk == photoTourId);
        if (allLogs) return result;
        return result.Take(2000);
    }

    [HttpGet("phototours")]
    public IEnumerable<PhotoTourInfo> GetPhotoTours()
    {
        return context.AutomaticPhotoTours
            .Include(apt => apt.PhotoTourEvents)
            .Select(apt => new PhotoTourInfo(apt.Name, apt.Finished, apt.Id, apt.PhotoTourEvents.Select(pte => pte.Timestamp).OrderBy(t => t).FirstOrDefault(),
            apt.PhotoTourEvents.Select(pte => pte.Timestamp).OrderByDescending(t => t).FirstOrDefault(), apt.IntervallInMinutes, apt.Comment, apt.PixelSizeInMm));
    }

    [HttpPost("startphototour")]
    public async Task StartAutomaticTour([FromBody] AutomaticTourStartInfo startInfo)
    {
        var (imagingDevice, temperatureDevices) = await CheckStartConditions(context, deviceFactory, startInfo.TemperatureMeasureDevice, startInfo.DeviceGuid, startInfo.MovementPlan, eventBus);

        if (!imagingDevice.Health.State.GetValueOrDefault().HasFlag(HealthState.ThermalCameraFunctional) && startInfo.ShouldUseIR)
            throw new Exception("IR camera was requested for photo tour, but not found");
        var photoTour = new DataModel.DataModel.AutomaticPhotoTour()
        {
            Comment = startInfo.Comment,
            Name = startInfo.Name,
            IntervallInMinutes = startInfo.IntervallInMinutes,
            DeviceId = Guid.Parse(startInfo.DeviceGuid),
            PixelSizeInMm = startInfo.PixelSizeInMm,
            TemperatureMeasurements = temperatureDevices
            .SelectMany(td => td.Sensors.Select(sensorId => new TemperatureMeasurement()
            {
                Comment = $"{td.DeviceHealth.Health.DeviceName}: {td.MeasurementInfo.Comment}",
                DeviceId = Guid.Parse(td.DeviceHealth.Health.DeviceId ?? throw new Exception($"Device {td.DeviceHealth.Ip} has no Device Id")),
                SensorId = sensorId,
                StartTime = DateTime.UtcNow
            })).PushIf(new TemperatureMeasurement()
            {
                Comment = $"{imagingDevice.Health.DeviceName}: IR-Temperature",
                DeviceId = Guid.Parse(startInfo.DeviceGuid),
                SensorId = TemperatureMeasurement.FlirLeptonSensorId,
                StartTime = DateTime.UtcNow
            }, _ => imagingDevice.Health.State.GetValueOrDefault().HasFlag(HealthState.ThermalCameraFunctional) && startInfo.ShouldUseIR)
            .ToList()
        };
        context.AutomaticPhotoTours.Add(photoTour);
        context.SaveChanges();
    }

    private static async Task<(DeviceHealthState ImagingDevice, List<(DeviceHealthState DeviceHealth, TemperatureMeasurementInfo MeasurementInfo, List<string> Sensors)> TemperatureDevices)> CheckStartConditions(
           IDataContext context, IDeviceApiFactory deviceFactory, IEnumerable<TemperatureMeasurementInfo> measurementDevices, string deviceGuid, long movementPlanId,
           IDeviceConnectionEventBus eventBus)
    {
        var deviceById = eventBus.GetDeviceHealthInformation()
            .Where(d => !d.Health.DeviceId.IsEmpty())
            .ToDictionary(d => d.Health.DeviceId ?? throw new Exception("DeviceId must not be empty"));
        if (!deviceById.TryGetValue(deviceGuid, out var imagingDevice)) throw new Exception($"Device {deviceGuid} could not be found");
        var recheckedDeviceHealth = await deviceFactory.HealthClient(imagingDevice.Ip).CheckdevicehealthAsync();
        if (recheckedDeviceHealth == default) throw new Exception("Imaging device could not be checked");
        imagingDevice.Health = recheckedDeviceHealth;
        if (!measurementDevices.All(td => deviceById.ContainsKey(td.Guid))) throw new Exception("Not all requested temperature measurement devices are available");
        var movementPlan = context.DeviceMovements.FirstOrDefault(dm => dm.Id == movementPlanId) ?? throw new Exception("Movementplan not found");
        if (!recheckedDeviceHealth.State.GetValueOrDefault().HasFlag(HealthState.NoirCameraFunctional)) throw new Exception($"{imagingDevice.Health.DeviceName} has no functioning vis camera");
        var temperatureDevices = measurementDevices
            .Select(td => (DeviceHealth: deviceById[td.Guid], MeasurementInfo: td, Sensors: new List<string>()))
            .ToList();
        foreach (var temperatureDevice in temperatureDevices)
        {
            var devices = await deviceFactory.TemperatureClient(temperatureDevice.DeviceHealth.Ip).DevicesAsync();
            temperatureDevice.Sensors.AddRange(devices);
        }
        var devicesWithoutSensor = temperatureDevices.Select(td => td.Sensors.Count == 0 ? $"{td.DeviceHealth.Health.DeviceName} has no temperature sensor" : "");
        if (devicesWithoutSensor.Any(d => !d.IsEmpty())) throw new Exception(devicesWithoutSensor.Concat("\n"));
        var alreadyOccupiedDevices = context.AutomaticPhotoTours
            .Where(pt => !pt.Finished)
            .SelectMany(pt => pt.TemperatureMeasurements.Select(tm => tm.DeviceId))
            .ToHashSet();
        foreach (var device in context.AutomaticPhotoTours.Where(pt => !pt.Finished).Select(pt => pt.DeviceId)) alreadyOccupiedDevices.Add(device);
        if (alreadyOccupiedDevices.Contains(Guid.Parse(deviceGuid))) throw new Exception("The imaging device is already busy with another photo tour");
        var busyTemperatureDevices = temperatureDevices
            .Select(td => alreadyOccupiedDevices.Contains(Guid.Parse(td.DeviceHealth.Health.DeviceId ?? "")) ? $"{td.DeviceHealth.Health.DeviceName} is used in another photo tour" : "");
        if (busyTemperatureDevices.Any(td => !td.IsEmpty())) throw new Exception(busyTemperatureDevices.Concat("\n"));
        return (imagingDevice, temperatureDevices);
    }
}
