using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.MotorMovement;

namespace PlantMonitorControl.Features.SwitchOutlets;

[ApiController]
[Route("api/[controller]")]
public class SwitchOutletsController(IOutletSwitcher switcher) : ControllerBase
{
    [HttpPost("switchoutlet")]
    public void SwitchOutlet(long code)
    {
        switcher.SwitchOutlet(code);
    }
}
