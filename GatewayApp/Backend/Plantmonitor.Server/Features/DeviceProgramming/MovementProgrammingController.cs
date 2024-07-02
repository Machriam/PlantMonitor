using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;

namespace Plantmonitor.Server.Features.DeviceProgramming;

[ApiController]
[Route("api/[controller]")]
public class MovementProgrammingController(IDataContext context)
{
    [HttpGet("getplan")]
    public DeviceMovement GetPlan(string deviceId)
    {
        var guid = Guid.Parse(deviceId);
        return context.DeviceMovements.FirstOrDefault(dm => dm.DeviceId == guid) ?? new();
    }

    [HttpPost("updateplan")]
    public void UpdatePlan([FromBody] DeviceMovement movement)
    {
        var existingPlan = context.DeviceMovements.FirstOrDefault(dm => dm.Id == movement.Id);
        if (existingPlan == null) context.DeviceMovements.Add(movement);
        else existingPlan.MovementPlan = movement.MovementPlan;
        context.SaveChanges();
    }
}
