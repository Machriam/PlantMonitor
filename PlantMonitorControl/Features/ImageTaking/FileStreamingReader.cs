using Plantmonitor.Shared.Features.ImageStreaming;

namespace PlantMonitorControl.Features.ImageTaking;

public interface IFileStreamingReader
{
    Task<FileInfo> ReadNextFile(string imagePath, int counter, CameraTypeInfo cameraInfo, CancellationToken token);

    Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, CameraTypeInfo cameraInfo, CancellationToken token);
}

public record struct FileInfo(DateTime CreationDate, int NewCounter, byte[]? FileData, int TemperatureInK)
{
    public readonly CameraStreamFormatter CreateFormatter(int stepCount)
    {
        return new()
        {
            PictureData = FileData,
            TemperatureInK = TemperatureInK,
            Steps = stepCount,
            Timestamp = CreationDate,
        };
    }
};

public class FileStreamingReader : IFileStreamingReader
{
    public const string CounterFormat = "000000";
    private static readonly string s_irEnding = CameraType.IR.Attribute<CameraTypeInfo>().FileEnding;

    public async Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, CameraTypeInfo cameraInfo, CancellationToken token)
    {
        counter = SkipFiles(imagePath, counter, howManyMoreRecentImagesMayExist, cameraInfo);
        return await ReadNextFile(imagePath, counter, cameraInfo, token);
    }

    public async Task<FileInfo> ReadNextFile(string imagePath, int counter, CameraTypeInfo cameraInfo, CancellationToken token)
    {
        var currentPath = Path.Combine(imagePath, counter.ToString(CounterFormat) + cameraInfo.FileEnding);
        if (Directory.GetFiles(imagePath, $"{(counter + 1).ToString(CounterFormat)}*{cameraInfo.FileEnding}").Length == 0) return new(default, counter, default, default);
        int temperatureInK = default;
        var bytesToSend = cameraInfo.FileEnding == s_irEnding ? currentPath.GetBytesFromIrFilePath(out temperatureInK) : await File.ReadAllBytesAsync(currentPath, token);
        var creationTime = File.GetCreationTimeUtc(currentPath);
        File.Delete(currentPath);
        return new(creationTime, counter + 1, bytesToSend, temperatureInK);
    }

    private static int SkipFiles(string imagePath, int counter, int skipCounter, CameraTypeInfo cameraInfo)
    {
        if (Directory.GetFiles(imagePath, $"{(counter + skipCounter).ToString(CounterFormat)}*{cameraInfo.FileEnding}").Length != 0)
        {
            for (var i = 0; i < skipCounter; i++)
            {
                var files = Directory.GetFiles(imagePath, $"{(counter + skipCounter)
                    .ToString(CounterFormat)}*{cameraInfo.FileEnding}");
                foreach (var file in files) File.Delete(file);
            }
            counter += skipCounter;
        }

        return counter;
    }
}
