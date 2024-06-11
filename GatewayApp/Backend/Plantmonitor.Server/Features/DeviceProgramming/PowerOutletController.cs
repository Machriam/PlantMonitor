using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceProgramming;

public record struct AssociatePowerOutletModel(Guid DeviceId, long? SwitchOnId, long? SwitchOffId);
public record struct OutletModel(long SwitchOnId, long SwitchOffId, string Name, int ButtonNumber, int Channel);

[ApiController]
[Route("api/[controller]")]
public class PowerOutletController(DataContext context)
{
    [HttpPost("updateassociatedoutlet")]
    public void AssociateDeviceWithPowerOutlet([FromBody] AssociatePowerOutletModel model)
    {
        context.DeviceSwitchAssociations.RemoveRange(context.DeviceSwitchAssociations.Where(ass => ass.DeviceId == model.DeviceId));
        if (model.SwitchOnId == null || model.SwitchOffId == null)
        {
            context.SaveChanges();
            return;
        }
        context.DeviceSwitchAssociations.Add(new DeviceSwitchAssociation()
        {
            DeviceId = model.DeviceId,
            OutletOnFk = model.SwitchOnId.Value,
            OutletOffFk = model.SwitchOffId.Value,
        });
        context.SaveChanges();
    }

    [HttpGet("outlets")]
    public IEnumerable<OutletModel> GetOutlets()
    {
        return context.SwitchableOutletCodes
            .GroupBy(c => new { c.OutletName, c.ChannelNumber, c.ChannelBaseNumber })
            .Select(c => new OutletModel(c.First(x => x.TurnsOn).Id, c.First(x => !x.TurnsOn).Id,
                        c.First().OutletName, c.First().ChannelNumber, c.First().ChannelBaseNumber));
    }

    [HttpGet("getoutlet")]
    public AssociatePowerOutletModel? PowerOutletForDevice(string deviceId)
    {
        var guid = Guid.Parse(deviceId);
        var result = context.DeviceSwitchAssociations.FirstOrDefault(ass => ass.DeviceId == guid);
        if (result == default) return null;
        return new(result.DeviceId, result.OutletOnFk, result.OutletOffFk);
    }

    [HttpPost("switchoutlet")]
    public void SwitchOutlet([FromServices] IDeviceApiFactory apiFactory, string ip, long code)
    {
        apiFactory.SwitchOutletsClient(ip).SwitchoutletAsync(code);
    }
}
