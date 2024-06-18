using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.DeviceControl;

[ApiController]
[Route("api/[controller]")]
public class DeviceController(IDeviceApiFactory apiFactory)
{
    [HttpGet("previewimage")]
    public async Task<IResult> PreviewImage(string ip, CameraType type)
    {
        var result = type == CameraType.Vis ? await apiFactory.VisImageTakingClient(ip).PreviewimageAsync() : await apiFactory.IrImageTakingClient(ip).PreviewimageAsync();
        var memoryStream = new MemoryStream();
        await result.Stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        return Results.File(memoryStream, "image/jpeg");
    }

    [HttpPost("killcamera")]
    public async Task KillCamera(string ip, CameraType type)
    {
        if (type == CameraType.Vis) await apiFactory.VisImageTakingClient(ip).KillcameraAsync();
        else await apiFactory.IrImageTakingClient(ip).KillcameraAsync();
    }

    [HttpPost("runffc")]
    public async Task RunFFC(string ip)
    {
        await apiFactory.IrImageTakingClient(ip).RunffcAsync();
    }

    [HttpGet("camerainfo")]
    public async Task<string> CameraInfo(string ip, CameraType type)
    {
        return type == CameraType.Vis ? await apiFactory.VisImageTakingClient(ip).CamerainfoAsync() : await apiFactory.IrImageTakingClient(ip).CamerainfoAsync();
    }

    [HttpGet("currentposition")]
    public async Task<int> CurrentPosition(string ip)
    {
        return await apiFactory.MovementClient(ip).CurrentpositionAsync();
    }

    [MotorAccessFilter]
    [HttpPost("zeroposition")]
    public async Task ZeroPosition(string ip)
    {
        await apiFactory.MovementClient(ip).ZeropositionAsync();
    }

    [MotorAccessFilter]
    [HttpPost("togglemotorengage")]
    public async Task ToggleMotorEngage(string ip, bool engage)
    {
        await apiFactory.MovementClient(ip).TogglemotorengageAsync(engage);
    }

    [MotorAccessFilter]
    [HttpPost("move")]
    public async Task Move(string ip, int steps, int minTime, int maxTime, int rampLength)
    {
        await apiFactory.MovementClient(ip).MovemotorAsync(steps, minTime, maxTime, rampLength);
    }
}
