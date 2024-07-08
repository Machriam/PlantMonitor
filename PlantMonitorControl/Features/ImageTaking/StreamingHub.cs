using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Features.ImageStreaming;
using PlantMonitorControl;
using PlantMonitorControl.Features.MotorMovement;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.ImageTaking;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop visCameraInterop,
    [FromKeyedServices(ICameraInterop.IrCamera)] ICameraInterop irCameraInterop,
    IFileStreamingReader fileStreamer, IMotorPositionCalculator motorPosition, ILogger<StreamingHub> logger) : Hub
{
    public async Task<ChannelReader<byte[]>> StreamIrData(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await irCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        ReadImagesFromFiles(channel, folder, data, irCameraInterop, token).RunInBackground(ex => ex.LogError());
        return channel.Reader;
    }

    public async Task<ChannelReader<byte[]>> StreamJpg(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await visCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        ReadImagesFromFiles(channel, folder, data, visCameraInterop, token).RunInBackground(ex => ex.LogError());
        return channel.Reader;
    }

    private static Channel<byte[]> CreateChannel(StreamingMetaData data)
    {
        return Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    private async Task ReadImagesFromFiles(Channel<byte[]> channel, string imagePath, StreamingMetaData data, ICameraInterop camera, CancellationToken token)
    {
        var typeInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
        while (data.StoreData && camera.CameraIsRunning())
        {
            await Task.Delay(50, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition().Position);
            var tickBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. tickBytes], token);
        }
        if (!data.StoreData)
        {
            await StreamLive(channel, imagePath, data, camera, token);
        }
        else
        {
            foreach (var file in Directory.EnumerateFiles(imagePath).OrderBy(x => x))
            {
                var fileCreationTime = File.GetCreationTimeUtc(file);
                var stepCount = motorPosition.StepForTime(new DateTimeOffset(fileCreationTime).ToUnixTimeMilliseconds());
                var bytesToSend = await fileStreamer.ReadFromFile(typeInfo, file, token);
                if (data.PositionsToStream.Contains(stepCount))
                {
                    await channel.Writer.WaitToWriteAsync(token);
                    await channel.Writer.WriteAsync(bytesToSend.CreateFormatter(stepCount).GetBytes(), token);
                }
            }
            channel.Writer.Complete();
        }
    }

    private async Task StreamLive(Channel<byte[]> channel, string imagePath, StreamingMetaData data, ICameraInterop camera, CancellationToken token)
    {
        var counter = 0;
        var typeInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
        while (true)
        {
            await Task.Delay(Random.Shared.Next(100, 400), token);
            if (!camera.CameraIsRunning()) break;
            var nextFile = await fileStreamer.ReadNextFileWithSkipping(imagePath, counter, 10, typeInfo, token);
            counter = nextFile.NewCounter;
            if (nextFile.FileData == null) continue;
            var currentPosition = motorPosition.CurrentPosition();
            logger.LogInformation("Sending image {counter}{ending} ", counter, typeInfo.FileEnding);
            await channel.Writer.WriteAsync(nextFile.CreateFormatter(currentPosition.Position).GetBytes(), token);
        }
    }
}
