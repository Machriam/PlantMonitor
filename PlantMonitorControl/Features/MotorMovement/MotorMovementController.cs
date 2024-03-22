using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration) : ControllerBase
{
    [HttpPost()]
    public async Task MoveMotor(int steps)
    {
        var rampFunction = steps.CreateRampFunction(1, 100);
        using var controller = new GpioController(PinNumberingScheme.Board);
        var pinout = configuration.MotorPinout;
        controller.OpenPin(pinout.Direction, PinMode.Output);
        controller.OpenPin(pinout.Enable, PinMode.Output);
        controller.OpenPin(pinout.Pulse, PinMode.Output);
        var left = PinValue.Low;
        var right = PinValue.High;
        var locked = PinValue.High;
        var released = PinValue.Low;

        controller.Write(pinout.Enable, locked);
        controller.Write(pinout.Direction, steps < 0 ? left : right);
        steps = Math.Abs(steps);
        for (var i = 0; i < steps; i++)
        {
            controller.Write(pinout.Pulse, PinValue.High);
            await Task.Delay((int)(rampFunction(i) * 0.5f));
            controller.Write(pinout.Pulse, PinValue.Low);
            await Task.Delay((int)(rampFunction(i) * 0.5f));
        }
        controller.Write(pinout.Enable, released);
    }
}