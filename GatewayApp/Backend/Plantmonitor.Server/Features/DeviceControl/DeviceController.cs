﻿using Microsoft.AspNetCore.Mvc;

namespace Plantmonitor.Server.Features.DeviceControl;

[ApiController]
[Route("api/[controller]")]
public class DeviceController(IDeviceApiFactory apiFactory)
{
    [HttpGet("previewimage")]
    public async Task<IResult> PreviewImage(string ip)
    {
        var result = await apiFactory.ImageTakingClient(ip).PreviewimageAsync();
        var memoryStream = new MemoryStream();
        await result.Stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        return Results.File(memoryStream, "image/jpeg");
    }

    [HttpPost("killcamera")]
    public async Task KillCamera(string ip)
    {
        await apiFactory.ImageTakingClient(ip).KillcameraAsync();
    }

    [HttpGet("camerainfo")]
    public async Task<string> CameraInfo(string ip)
    {
        return await apiFactory.ImageTakingClient(ip).CamerainfoAsync();
    }
    [HttpGet("currentposition")]
    public async Task<int> CurrentPosition(string ip)
    {
        return await apiFactory.MovementClient(ip).CurrentpositionAsync();
    }

    [HttpPost("togglemotorengage")]
    public async Task ToggleMotorEngage(string ip, bool engage)
    {
        await apiFactory.MovementClient(ip).TogglemotorengageAsync(engage);
    }

    [HttpPost("move")]
    public async Task Move(string ip, int steps, int minTime, int maxTime, int rampLength)
    {
        await apiFactory.MovementClient(ip).MovemotorAsync(steps, minTime, maxTime, rampLength);
    }
}