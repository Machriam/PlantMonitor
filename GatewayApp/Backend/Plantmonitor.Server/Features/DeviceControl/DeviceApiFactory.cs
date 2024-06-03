namespace Plantmonitor.Server.Features.DeviceControl;

public interface IDeviceApiFactory
{
    IVisImageTakingClient VisImageTakingClient(string ip);

    IIrImageTakingClient IrImageTakingClient(string ip);

    IMotorMovementClient MovementClient(string ip);

    ITemperatureClient TemperatureClient(string ip);

    IHealthClient HealthClient(string ip);
}

public class DeviceApiFactory : IDeviceApiFactory
{
    public ITemperatureClient TemperatureClient(string ip)
    {
        return new TemperatureClient($"https://{ip}");
    }

    public IHealthClient HealthClient(string ip)
    {
        return new HealthClient($"https://{ip}");
    }

    public IIrImageTakingClient IrImageTakingClient(string ip)
    {
        return new IrImageTakingClient($"https://{ip}");
    }

    public IVisImageTakingClient VisImageTakingClient(string ip)
    {
        return new VisImageTakingClient($"https://{ip}");
    }

    public IMotorMovementClient MovementClient(string ip)
    {
        return new MotorMovementClient($"https://{ip}");
    }
}
