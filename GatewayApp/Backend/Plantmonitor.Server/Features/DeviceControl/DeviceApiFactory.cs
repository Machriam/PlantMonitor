namespace Plantmonitor.Server.Features.DeviceControl;

public interface IDeviceApiFactory
{
    IImageTakingClient ImageTakingClient(string ip);
    IMotorMovementClient MovementClient(string ip);
}
public class DeviceApiFactory : IDeviceApiFactory
{
    public IImageTakingClient ImageTakingClient(string ip)
    {
        return new ImageTakingClient($"https://{ip}");
    }

    public IMotorMovementClient MovementClient(string ip)
    {
        return new MotorMovementClient($"https://{ip}");
    }
}
