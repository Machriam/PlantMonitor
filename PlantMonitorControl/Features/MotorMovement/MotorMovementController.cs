using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration) : ControllerBase
{
    [HttpPost()]
    public void MoveMotor(int steps, int minTime, int maxTime, int rampLength)
    {
        var sw = new Stopwatch();
        var microSecondsPerTick = 1000d * 1000d / Stopwatch.Frequency;
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
        var rampFunction = steps.CreateLogisticRampFunction(minTime, maxTime, rampLength);
        for (var i = 0; i < steps; i++)
        {
            var delay = (int)(rampFunction(i) * 0.5f);
            controller.Write(pinout.Pulse, PinValue.High);
            sw.Restart();
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
            controller.Write(pinout.Pulse, PinValue.Low);
            sw.Restart();
            while (sw.ElapsedTicks * microSecondsPerTick < delay) { }
        }
        controller.Write(pinout.Enable, released);
    }
}