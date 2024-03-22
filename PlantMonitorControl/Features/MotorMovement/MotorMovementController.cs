using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class MotorMovementController : ControllerBase
{
    [HttpPost()]
    public void MoveMotor()
    {
    }
}