using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MotorMovement;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop visCameraInterop,
    [FromKeyedServices(ICameraInterop.IrCamera)] ICameraInterop irCameraInterop,
    IFileStreamingReader fileStreamer, IMotorPositionCalculator motorPosition) : Hub
{
    public async Task<ChannelReader<byte[]>> StreamIrData(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await irCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        _ = ReadImagesFromFiles(channel, folder, data, irCameraInterop, token);
        return channel.Reader;
    }

    public async Task<ChannelReader<byte[]>> StreamStoredMjpeg(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await visCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        _ = ReadImagesFromFiles(channel, folder, data, visCameraInterop, token);
        return channel.Reader;
    }

    private static Channel<byte[]> CreateChannel(StreamingMetaData data)
    {
        return Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = data.StoreData ? BoundedChannelFullMode.Wait : BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    private async Task ReadImagesFromFiles(Channel<byte[]> channel, string imagePath, StreamingMetaData data, ICameraInterop camera, CancellationToken token)
    {
        while (data.StoreData && camera.CameraIsRunning())
        {
            await Task.Delay(50, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            var tickBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. tickBytes], token);
        }
        if (!data.StoreData)
        {
            await StreamLive(channel, imagePath, camera, token);
        }
        else
        {
            foreach (var file in Directory.EnumerateFiles(imagePath).OrderBy(x => x))
            {
                var fileCreationTime = File.GetCreationTimeUtc(file);
                var creationTimeBytes = BitConverter.GetBytes(fileCreationTime.Ticks);
                var stepCount = motorPosition.StepForTime(new DateTimeOffset(fileCreationTime).ToUnixTimeMilliseconds());
                var bytesOfStep = BitConverter.GetBytes(stepCount);
                var bytesToSend = await File.ReadAllBytesAsync(file, token);
                if (data.PositionsToStream.Contains(stepCount))
                {
                    await channel.Writer.WaitToWriteAsync(token);
                    await channel.Writer.WriteAsync([.. bytesOfStep, .. creationTimeBytes, .. bytesToSend], token);
                }
                File.Delete(file);
            }
        }
    }

    private async Task StreamLive(Channel<byte[]> channel, string imagePath, ICameraInterop camera, CancellationToken token)
    {
        var counter = 0;
        while (true)
        {
            await Task.Delay(10, token);
            if (!camera.CameraIsRunning()) break;
            (var creationTime, counter, var bytesToSend) = await fileStreamer.ReadNextFileWithSkipping(imagePath, counter, 10, token);
            if (bytesToSend == null) continue;
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition());
            var creationTimeBytes = BitConverter.GetBytes(creationTime.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. creationTimeBytes, .. bytesToSend], token);
        }
    }
}
