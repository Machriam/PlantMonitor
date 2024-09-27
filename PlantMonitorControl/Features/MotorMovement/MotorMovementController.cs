using Microsoft.AspNetCore.Mvc;

namespace PlantMonitorControl.Features.MotorMovement;

public record struct MotorPosition(bool Engaged, int Position, bool Dirty);

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
    public MotorPosition CurrentPosition()
    {
        return motorPosition.CurrentPosition();
    }

    [HttpPost("movemotor")]
    public async Task MoveMotor(int steps, int minTime, int maxTime, int rampLength, int maxAllowedPosition, int minAllowedPosition)
    {
        await motorPosition.MoveMotor(steps, minTime, maxTime, rampLength, maxAllowedPosition, minAllowedPosition);
    }
}
