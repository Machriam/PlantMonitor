using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("api/[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration, IMotorPositionCalculator motorPosition) : ControllerBase
{
    [HttpPost("togglemotorengage")]
    public void ToggleMotorEngage(bool shouldEngage)
    {
        var locked = PinValue.Low;
        var released = PinValue.High;
        using var controller = new GpioController(PinNumberingScheme.Board);
        var pinout = configuration.MotorPinout;
        controller.OpenPin(pinout.Enable, PinMode.Output);
        controller.Write(pinout.Enable, shouldEngage ? locked : released);
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
        var sw = new Stopwatch();
        var microSecondsPerTick = 1000d * 1000d / Stopwatch.Frequency;
        using var controller = new GpioController(PinNumberingScheme.Board);
        var pinout = configuration.MotorPinout;
        controller.OpenPin(pinout.Direction, PinMode.Output);
        controller.OpenPin(pinout.Pulse, PinMode.Output);
        var left = PinValue.High;
        var right = PinValue.Low;

        controller.Write(pinout.Direction, steps < 0 ? left : right);
        var stepUnit = steps < 0 ? -1 : 1;
        var stepsToMove = Math.Abs(steps);
        var rampFunction = stepsToMove.CreateLogisticRampFunction(minTime, maxTime, rampLength);
        for (var i = 0; i < stepsToMove; i++)
        {
            var delay = (int)(rampFunction(i) * 0.5f);
            controller.Write(pinout.Pulse, PinValue.High);
            sw.Restart();
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
            controller.Write(pinout.Pulse, PinValue.Low);
            sw.Restart();
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
            motorPosition.UpdatePosition(stepUnit);
        }
        motorPosition.PersistCurrentPosition();
    }
}
