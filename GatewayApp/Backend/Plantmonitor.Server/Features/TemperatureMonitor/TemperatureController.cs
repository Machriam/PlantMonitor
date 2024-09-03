using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.MeasureTemperature;

namespace Plantmonitor.Server.Features.TemperatureMonitor;

[ApiController]
[Route("api/[controller]")]
public class TemperatureController(IDeviceApiFactory apiFactory, ITemperatureMeasurementWorker worker, IDataContext dataContext)
{
    public record struct MeasurementTemperatureDatum(float Temperature, DateTime Timestamp);
    public record struct MeasurementStartInfo(MeasurementDevice[] Devices, string Ip);
    public record struct RunningMeasurement(string Ip, long MeasurementId);

    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices(string ip)
    {
        return await apiFactory.TemperatureClient(ip).DevicesAsync();
    }

    [HttpGet("runningmeasurements")]
    public IEnumerable<RunningMeasurement> GetRunningMeasurements()
    {
        return worker.GetRunningMeasurements().Select(m => new RunningMeasurement(m.Ip, m.MeasurementId));
    }

    [HttpGet("measurements")]
    public IEnumerable<TemperatureMeasurement> Measurements()
    {
        return dataContext.TemperatureMeasurements;
    }

    [HttpGet("temperatureofmeasurement")]
    public IEnumerable<MeasurementTemperatureDatum> TemperaturesOfMeasurement(long measurementId)
    {
        var measurement = dataContext.TemperatureMeasurements
            .Include(tm => tm.TemperatureMeasurementValues)
            .Where(tm => tm.Id == measurementId);
        return measurement
            .SelectMany(tv => tv.TemperatureMeasurementValues.Select(mv => new MeasurementTemperatureDatum(mv.Temperature, mv.Timestamp)))
            .ToList()
            .OrderBy(tv => tv.Timestamp);
    }

    [HttpPost("addmeasurement")]
    public async Task AddMeasurement([FromBody] MeasurementStartInfo info)
    {
        await worker.StartTemperatureMeasurement(info.Devices, info.Ip);
    }

    [HttpPost("stopmeasurement")]
    public void StopMeasurement(string ip)
    {
        worker.StopMeasurement(ip);
    }
}
