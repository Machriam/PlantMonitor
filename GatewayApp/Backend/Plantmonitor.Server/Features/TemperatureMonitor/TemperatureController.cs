using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Features.MeasureTemperature;

namespace Plantmonitor.Server.Features.TemperatureMonitor;

[ApiController]
[Route("api/[controller]")]
public class TemperatureController(IDeviceApiFactory apiFactory, ITemperatureMeasurementWorker worker, DataContext dataContext)
{
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

    [HttpPost("addmeasurement")]
    public void AddMeasurement([FromBody] MeasurementStartInfo info)
    {
        worker.StartTemperatureMeasurement(info.Devices, info.Ip);
    }

    [HttpPost("stopmeasurement")]
    public void StopMeasurement(string ip)
    {
        worker.StopMeasurement(ip);
    }
}
