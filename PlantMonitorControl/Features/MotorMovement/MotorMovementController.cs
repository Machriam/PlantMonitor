using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration) : ControllerBase
{
    [HttpPost()]
    public async Task MoveMotor()
    {
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
        controller.Write(pinout.Direction, left);
        for (var i = 0; i < 10; i++)
        {
            controller.Write(pinout.Pulse, PinValue.High);
            await Task.Delay(100);
            controller.Write(pinout.Pulse, PinValue.Low);
        }
        controller.Write(pinout.Enable, released);
    }
}