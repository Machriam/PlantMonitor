using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration) : ControllerBase
{
    [HttpPost()]
    public void MoveMotor(bool direction, bool enable, bool pulse)
    {
        using var controller = new GpioController(PinNumberingScheme.Board);
        var pinout = configuration.MotorPinout;
        controller.OpenPin(pinout.Direction);
        controller.OpenPin(pinout.Enable);
        controller.OpenPin(pinout.Pulse);

        controller.Write(pinout.Direction, direction ? PinValue.High : PinValue.Low);
        controller.Write(pinout.Enable, enable ? PinValue.High : PinValue.Low);
        controller.Write(pinout.Pulse, pulse ? PinValue.High : PinValue.Low);
    }
}