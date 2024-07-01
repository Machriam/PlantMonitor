using Plantmonitor.Shared.Features.ImageStreaming;

namespace PlantMonitorControl.Features.ImageTaking;

public interface IFileStreamingReader
{
    Task<FileInfo> ReadNextFile(string imagePath, int counter, CameraTypeInfo cameraInfo, CancellationToken token);

    Task<FileInfo> ReadFromFile(CameraTypeInfo cameraInfo, string currentPath, CancellationToken token, bool deleteFile = true);

    Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int maxImageCount, CameraTypeInfo cameraInfo, CancellationToken token);
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

public class FileStreamingReader(ILogger<FileStreamingReader> logger) : IFileStreamingReader
{
    public const string CounterFormat = "000000";
    private static readonly string s_irEnding = CameraType.IR.Attribute<CameraTypeInfo>().FileEnding;

    public async Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int maxImageCount, CameraTypeInfo cameraInfo, CancellationToken token)
    {
        var lastCounter = -1;
        while (counter != lastCounter)
        {
            lastCounter = counter;
            counter = SkipFiles(imagePath, counter, maxImageCount, cameraInfo);
        }
        return await ReadNextFile(imagePath, counter, cameraInfo, token);
    }

    public async Task<FileInfo> ReadNextFile(string imagePath, int counter, CameraTypeInfo cameraInfo, CancellationToken token)
    {
        var currentPath = Directory.GetFiles(imagePath, $"{counter.ToString(CounterFormat)}*{cameraInfo.FileEnding}").FirstOrDefault();
        if (currentPath == null || Directory.GetFiles(imagePath, $"{(counter + 1).ToString(CounterFormat)}*{cameraInfo.FileEnding}").Length == 0) return new(default, counter, default, default);
        var result = await ReadFromFile(cameraInfo, currentPath, token);
        result.NewCounter = ++counter;
        return result;
    }

    public async Task<FileInfo> ReadFromFile(CameraTypeInfo cameraInfo, string currentPath, CancellationToken token, bool deleteFile = true)
    {
        try
        {
            int temperatureInK = default;
            var bytesToSend = cameraInfo.FileEnding == s_irEnding ? currentPath.GetBytesFromIrFilePath(out temperatureInK) : await File.ReadAllBytesAsync(currentPath, token);
            var creationTime = File.GetCreationTimeUtc(currentPath);
            if (deleteFile) File.Delete(currentPath);
            return new(creationTime, 0, bytesToSend, temperatureInK);
        }
        catch (Exception ex)
        {
            logger.LogError("Could not read from file: {error}\n{stacktrace}", ex.Message, ex.StackTrace);
            return default;
        }
    }

    private int SkipFiles(string imagePath, int counter, int skipCounter, CameraTypeInfo cameraInfo)
    {
        if (Directory.GetFiles(imagePath, $"{(counter + skipCounter).ToString(CounterFormat)}*{cameraInfo.FileEnding}").Length != 0)
        {
            try
            {
                for (var i = 0; i < skipCounter; i++)
                {
                    var files = Directory.GetFiles(imagePath, $"{(counter + i).ToString(CounterFormat)}*{cameraInfo.FileEnding}");
                    foreach (var file in files) File.Delete(file);
                }
                counter += skipCounter;
            }
            catch (Exception ex)
            {
                logger.LogError("Filestream error: {error}\n{stacktrace}", ex.Message, ex.StackTrace);
            }
        }

        return counter;
    }
}
