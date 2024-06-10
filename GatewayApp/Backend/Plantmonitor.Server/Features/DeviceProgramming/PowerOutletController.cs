using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceProgramming;

public record struct AssociatePowerOutletModel(Guid DeviceId, long SwitchOnId, long SwitchOffId);

[ApiController]
[Route("api/[controller]")]
public class PowerOutletController(DataContext context)
{
    [HttpPost("updateassociatedoutlet")]
    public void AssociateDeviceWithPowerOutlet([FromBody] AssociatePowerOutletModel model)
    {
        context.DeviceSwitchAssociations.RemoveRange(context.DeviceSwitchAssociations.Where(ass => ass.DeviceId == model.DeviceId));
        context.DeviceSwitchAssociations.Add(new DeviceSwitchAssociation()
        {
            DeviceId = model.DeviceId,
            OutletOnFk = model.SwitchOnId,
            OutletOffFk = model.SwitchOffId,
        });
        context.SaveChanges();
    }

    [HttpGet("getoutlet")]
    public AssociatePowerOutletModel PowerOutletForDevice(string deviceId)
    {
        var guid = Guid.Parse(deviceId);
        var result = context.DeviceSwitchAssociations.First(ass => ass.DeviceId == guid);
        return new(result.DeviceId, result.OutletOnFk, result.OutletOffFk);
    }

    [HttpPost("switchoutlet")]
    public void SwitchOutlet([FromServices] IDeviceApiFactory apiFactory, string ip, long code)
    {
        apiFactory.SwitchOutletsClient(ip).SwitchoutletAsync(code);
    }
}
