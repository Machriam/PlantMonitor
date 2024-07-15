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
    public record struct AutomaticTourStartInfo(float IntervallInMinutes, long MovementPlan, TemperatureMeasurementInfo[] TemperatureMeasureDevice, string Comment, string Name, string DeviceGuid);
    public record struct PhotoTourInfo(string Name, bool Finished, long Id, DateTime FirstEvent, DateTime LastEvent);

    [HttpPost("stopphototour")]
    public void StopPhotoTour(long id)
    {
        context.AutomaticPhotoTours.First(pt => pt.Id == id).Finished = true;
        context.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            PhotoTourFk = id,
            Timestamp = DateTime.UtcNow,
            Type = PhotoTourEventType.Information,
            Message = "Photo tour finished",
        });
        context.SaveChanges();
    }

    [HttpGet("events")]
    public IEnumerable<PhotoTourEvent> GetEvents(long photoTourId)
    {
        return context.PhotoTourEvents
            .OrderByDescending(pte => pte.Timestamp)
            .Where(pte => pte.PhotoTourFk == photoTourId);
    }

    [HttpGet("phototours")]
    public IEnumerable<PhotoTourInfo> GetPhotoTours()
    {
        return context.AutomaticPhotoTours
            .Include(apt => apt.PhotoTourEvents)
            .Select(apt => new PhotoTourInfo(apt.Name, apt.Finished, apt.Id, apt.PhotoTourEvents.Min(pte => pte.Timestamp), apt.PhotoTourEvents.Max(pte => pte.Timestamp)));
    }

    [HttpPost("startphototour")]
    public async Task StartAutomaticTour([FromBody] AutomaticTourStartInfo startInfo)
    {
        var deviceById = eventBus.GetDeviceHealthInformation()
            .Where(d => !d.Health.DeviceId.IsEmpty())
            .ToDictionary(d => d.Health.DeviceId ?? throw new Exception("DeviceId must not be empty"));
        if (!deviceById.TryGetValue(startInfo.DeviceGuid, out var imagingDevice)) throw new Exception($"Device {startInfo.DeviceGuid} could not be found");
        if (!startInfo.TemperatureMeasureDevice.All(td => deviceById.ContainsKey(td.Guid))) throw new Exception("Not all requested temperature measurement devices are available");
        var movementPlan = context.DeviceMovements.FirstOrDefault(dm => dm.Id == startInfo.MovementPlan) ?? throw new Exception("Movementplan not found");
        if (!imagingDevice.Health.State.GetValueOrDefault().HasFlag(HealthState.NoirCameraFunctional)) throw new Exception($"{imagingDevice.Health.DeviceName} has no functioning vis camera");
        var temperatureDevices = startInfo.TemperatureMeasureDevice
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
        if (alreadyOccupiedDevices.Contains(Guid.Parse(startInfo.DeviceGuid))) throw new Exception("The imaging device is already busy with another photo tour");
        var busyTemperatureDevices = temperatureDevices
            .Select(td => alreadyOccupiedDevices.Contains(Guid.Parse(td.DeviceHealth.Health.DeviceId ?? "")) ? $"{td.DeviceHealth.Health.DeviceName} is used in another photo tour" : "");
        if (busyTemperatureDevices.Any(td => !td.IsEmpty())) throw new Exception(busyTemperatureDevices.Concat("\n"));

        var photoTour = new DataModel.DataModel.AutomaticPhotoTour()
        {
            Comment = startInfo.Comment,
            Name = startInfo.Name,
            IntervallInMinutes = startInfo.IntervallInMinutes,
            DeviceId = Guid.Parse(startInfo.DeviceGuid),
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
            }, _ => imagingDevice.Health.State.GetValueOrDefault().HasFlag(HealthState.ThermalCameraFunctional))
            .ToList()
        };
        context.AutomaticPhotoTours.Add(photoTour);
        context.SaveChanges();
    }
}
