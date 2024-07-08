using System.Device.Gpio;
using System.Reflection.Metadata.Ecma335;

namespace PlantMonitorControl.Features.MotorMovement;

public interface IGpioInteropFactory
{
    IGpioInterop Create();
}

public interface IGpioInterop : IDisposable
{
    void OpenPin(int pin, PinMode mode);

    void Write(int pinNumber, PinValue value);
}

public class GpioInteropFactory : IGpioInteropFactory
{
    public IGpioInterop Create() => new GpioInterop(new GpioController(PinNumberingScheme.Board));
}

public class GpioInteropFactoryDevelop : IGpioInteropFactory
{
    public IGpioInterop Create() => new GpioInteropDevelop();
}

public class GpioInterop(GpioController controller) : IGpioInterop
{
    public void Dispose()
    {
        controller?.Dispose();
    }

    public void OpenPin(int pin, PinMode mode) => controller.OpenPin(pin, mode);

    public void Write(int pinNumber, PinValue value) => controller.Write(pinNumber, value);
}

public class GpioInteropDevelop() : IGpioInterop
{
    public void Dispose()
    { }

    public void OpenPin(int pin, PinMode mode)
    { }

    public void Write(int pinNumber, PinValue value)
    { }
}
