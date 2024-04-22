using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using System.Device.Gpio;
using System.Diagnostics;

namespace PlantMonitorControl.Features.MotorMovement;

[ApiController]
[Route("api/[controller]")]
public class MotorMovementController(IEnvironmentConfiguration configuration) : ControllerBase
{
    private const string CurrentPositionFile = "currentPosition.txt";
    private static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), CurrentPositionFile);

    [HttpPost("togglemotorengage")]
    public void ToggleMotorEngage(bool shouldEngage)
    {
        var locked = PinValue.High;
        var released = PinValue.Low;
        using var controller = new GpioController(PinNumberingScheme.Board);
        var pinout = configuration.MotorPinout;
        controller.OpenPin(pinout.Enable, PinMode.Output);
        controller.Write(pinout.Enable, shouldEngage ? locked : released);
    }

    [HttpGet("currentposition")]
    public int CurrentPosition()
    {
        return System.IO.File.Exists(_filePath) ? int.Parse(System.IO.File.ReadAllText(_filePath)) : 0;
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
        var left = PinValue.Low;
        var right = PinValue.High;

        controller.Write(pinout.Direction, steps < 0 ? left : right);
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
        }
        var currentPosition = CurrentPosition();
        System.IO.File.WriteAllText(_filePath, (currentPosition + steps).ToString());
    }
}