using Iot.Device.GrovePiDevice;
using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Features.ImageStreaming;
using PlantMonitorControl.Features.MotorMovement;
using System.IO.Compression;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.ImageTaking;

public class StreamingHub([FromKeyedServices(ICameraInterop.VisCamera)] ICameraInterop visCameraInterop,
    [FromKeyedServices(ICameraInterop.IrCamera)] ICameraInterop irCameraInterop,
    IFileStreamingReader fileStreamer, IMotorPositionCalculator motorPosition, ILogger<StreamingHub> logger) : Hub
{
    private static readonly string s_streamArchive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "streamArchive");

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

    public async Task<ChannelReader<byte[]>> StoreIrData(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await irCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        StoreImages(channel, folder, data, irCameraInterop, token).RunInBackground(ex => ex.LogError());
        return channel.Reader;
    }

    public async Task<ChannelReader<byte[]>> StoreJpg(StreamingMetaData data, CancellationToken token)
    {
        motorPosition.ResetHistory();
        var channel = CreateChannel(data);
        var folder = await visCameraInterop.StreamPictureDataToFolder(data.ResolutionDivider, data.Quality, data.DistanceInM);
        StoreImages(channel, folder, data, visCameraInterop, token).RunInBackground(ex => ex.LogError());
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
    private static string GetStreamArchive()
    {
        if (!Path.Exists(s_streamArchive)) Directory.CreateDirectory(s_streamArchive);
        return s_streamArchive;
    }

    private async Task StoreImages(Channel<byte[]> channel, string imagePath, StreamingMetaData data, ICameraInterop camera, CancellationToken token)
    {
        var typeInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
        logger.LogInformation("Reading images from file type: {type}, live: {live}", data.Type, !data.StoreData);
        while (camera.CameraIsRunning())
        {
            await Task.Delay(100, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition().Position);
            var tickBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. tickBytes], token);
        }
        var history = motorPosition.GetHistory();
        File.WriteAllText($"{imagePath}/positionHistory.json", history);
        var timeData = Directory.EnumerateFiles(imagePath)
            .Select(f => new { File = f, CreationDate = new DateTimeOffset(File.GetCreationTimeUtc(f)).ToUnixTimeMilliseconds() })
            .AsJson();
        File.WriteAllText($"{imagePath}/positionByTime.json", timeData);
        ZipFile.CreateFromDirectory(imagePath, $"{GetStreamArchive()}/{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss-fff}_{typeInfo.SignalRMethod}.zip");
        Directory.Delete(imagePath, true);
        await channel.Writer.WriteAsync(CameraStreamFormatter.FinishSignal, token);
        channel.Writer.Complete();
    }

    private async Task ReadImagesFromFiles(Channel<byte[]> channel, string imagePath, StreamingMetaData data, ICameraInterop camera, CancellationToken token)
    {
        var typeInfo = data.GetCameraType().Attribute<CameraTypeInfo>();
        logger.LogInformation("Reading images from file type: {type}, live: {live}", data.Type, !data.StoreData);
        while (data.StoreData && camera.CameraIsRunning())
        {
            await Task.Delay(100, token);
            var steps = BitConverter.GetBytes(motorPosition.CurrentPosition().Position);
            var tickBytes = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            await channel.Writer.WriteAsync([.. steps, .. tickBytes], token);
        }
        if (!data.StoreData)
        {
            logger.LogInformation("Start live streaming");
            await StreamLive(channel, imagePath, data, camera, token);
        }
        else
        {
            logger.LogInformation("Start file reading");
            var possibleImages = Directory.EnumerateFiles(imagePath)
                .OrderBy(x => x)
                .Select(file =>
                {
                    var fileCreationTime = File.GetCreationTimeUtc(file);
                    var stepCount = motorPosition.StepForTime(new DateTimeOffset(fileCreationTime).ToUnixTimeMilliseconds());
                    Func<Task<FileInfo>> BytesToSend() => async () => await fileStreamer.ReadFromFile(typeInfo, file, token);
                    return (StepCount: stepCount, CreationTime: fileCreationTime, File: file, BytesToSend: BytesToSend());
                })
                .Where(x => data.PositionsToStream.Contains(x.StepCount))
                .ToList();
            var lastCalibrationTimes = camera.LastCalibrationTimes().OrderBy(c => c).ToList();
            string FormatFileInfo((string Name, FileInfo FileInfo) fileInfo) => $"{fileInfo.Name}, {fileInfo.FileInfo.CreationDate:HH:mm:ss}";
            foreach (var group in possibleImages.GroupBy(pi => pi.StepCount))
            {
                logger.LogInformation("Sending {type} image of step {step}", data.GetCameraType(), group.Key);
                if (data.GetCameraType() == CameraType.IR)
                {
                    var fileInfos = new List<(string Name, FileInfo Info)>();
                    foreach (var item in group)
                    {
                        fileInfos.Add((item.File, await item.BytesToSend()));
                    }
                    var firstImageInGroup = fileInfos.MinBy(fi => fi.Info.CreationDate);
                    var (timeDiff, calibration) = lastCalibrationTimes
                        .Select(ct => (TimeDiff: firstImageInGroup.Info.CreationDate - ct, Calibration: ct))
                        .MinBy(ct => Math.Abs(ct.TimeDiff.TotalMilliseconds));
                    logger.LogInformation("Last calibration time: {calibration} with time to first image: {timeDiff} ms", calibration.ToString("yyyy-MM-dd_HH:mm:ss"), timeDiff.TotalMilliseconds);
                    var calibrationFinished = firstImageInGroup.Info.CreationDate;
                    if (calibration != default) calibrationFinished = calibration.AddSeconds(2);
                    logger.LogInformation("Applicable ir-images: {from} to {end}", FormatFileInfo(fileInfos.FirstOrDefault()), FormatFileInfo(fileInfos.LastOrDefault()));
                    var calibratedImages = fileInfos.Where(fi => fi.Info.CreationDate > calibrationFinished).ToList();
                    if (calibratedImages.Count == 0)
                    {
                        logger.LogError("No calibrated image found for step: {step}", group.Key);
                        continue;
                    }
                    var (bestName, bestInfo) = calibratedImages.MaxBy(fi => fi.Info.TemperatureSum);
                    logger.LogInformation("Sending file: {file}", bestName);
                    await channel.Writer.WaitToWriteAsync(token);
                    await channel.Writer.WriteAsync(bestInfo.CreateFormatter(group.Key).GetBytes(), token);
                }
                else if (data.GetCameraType() == CameraType.Vis)
                {
                    logger.LogInformation("Possible Vis-images: {count}", group.Count());
                    if (!group.Any())
                    {
                        logger.LogError("Skipping because of no Vis-images");
                        continue;
                    }
                    logger.LogInformation("Vis-images from {from} to {to}", group.First().File, group.Last().File);
                    var bestImage = group.OrderBy(pi => pi.CreationTime).TakeLast(2).FirstOrDefault();
                    var bytesToSend = await bestImage.BytesToSend();
                    logger.LogInformation("Sending file: {file}", bestImage.File);
                    await channel.Writer.WaitToWriteAsync(token);
                    await channel.Writer.WriteAsync(bytesToSend.CreateFormatter(group.Key).GetBytes(), token);
                }
            }
            logger.LogInformation("Deleting all files of type: {type}", Enum.GetName(data.GetCameraType()));
            foreach (var file in Directory.EnumerateFiles(imagePath)) File.Delete(file);
            logger.LogInformation("Closing channel writer {type}", Enum.GetName(data.GetCameraType()));
            await channel.Writer.WriteAsync(CameraStreamFormatter.FinishSignal, token);
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
