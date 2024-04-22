using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;

namespace Plantmonitor.Server.Features.DeviceProgramming;

[ApiController]
[Route("api/[controller]")]
public class MovementProgrammingController(DataContext context)
{
    [HttpGet("getplan")]
    public DeviceMovement GetPlan(string deviceId)
    {
        var guid = Guid.Parse(deviceId);
        return context.DeviceMovements.FirstOrDefault(dm => dm.DeviceId == guid) ?? new();
    }

    [HttpPost("addplan")]
    public void AddPlan([FromBody] DeviceMovement movement)
    {
        context.DeviceMovements.Add(movement);
        context.SaveChanges();
    }

    [HttpPost("updateplan")]
    public void UpdatePlan([FromBody] DeviceMovement movement)
    {
        context.DeviceMovements.First(dm => dm.Id == movement.Id).MovementPlan = movement.MovementPlan;
        context.SaveChanges();
    }
}