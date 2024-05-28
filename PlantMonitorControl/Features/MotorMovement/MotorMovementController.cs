using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("api/[controller]")]
public class MotorMovementController(IMotorPositionCalculator motorPosition) : ControllerBase
{
    [HttpPost("togglemotorengage")]
    public void ToggleMotorEngage(bool shouldEngage)
    {
        motorPosition.ToggleMotorEngage(shouldEngage);
    }

    [HttpPost("zeroposition")]
    public void ZeroCurrentPosition()
    {
        motorPosition.ZeroPosition();
    }

    [HttpGet("currentposition")]
    public int CurrentPosition()
    {
        return motorPosition.CurrentPosition();
    }

    [HttpPost("movemotor")]
    public void MoveMotor(int steps, int minTime, int maxTime, int rampLength)
    {
        motorPosition.MoveMotor(steps, minTime, maxTime, rampLength);
    }
}
