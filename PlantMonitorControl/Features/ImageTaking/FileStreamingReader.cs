using Plantmonitor.Shared.Features.ImageStreaming;

namespace PlantMonitorControl.Features.ImageTaking;

public interface IFileStreamingReader
{
    Task<FileInfo> ReadNextFile(string imagePath, int counter, string fileEnding, CancellationToken token);

    Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, string fileEnding, CancellationToken token);
}

public record struct FileInfo(DateTime CreationDate, int NewCounter, byte[]? FileData);

public class FileStreamingReader : IFileStreamingReader
{
    public const string CounterFormat = "000000";
    private static readonly string s_irEnding = CameraType.IR.Attribute<CameraTypeInfo>().FileEnding;

    public async Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, string fileEnding, CancellationToken token)
    {
        counter = SkipFiles(imagePath, counter, howManyMoreRecentImagesMayExist, fileEnding);
        return await ReadNextFile(imagePath, counter, fileEnding, token);
    }

    public async Task<FileInfo> ReadNextFile(string imagePath, int counter, string fileEnding, CancellationToken token)
    {
        var currentPath = Path.Combine(imagePath, counter.ToString(CounterFormat) + fileEnding);
        var nextPath = Path.Combine(imagePath, (counter + 1).ToString(CounterFormat) + fileEnding);
        if (!Path.Exists(nextPath)) return new(default, counter, default);
        var bytesToSend = fileEnding == s_irEnding ? currentPath.GetBytesFromIrFilePath() : await File.ReadAllBytesAsync(currentPath, token);
        var creationTime = File.GetCreationTimeUtc(currentPath);
        File.Delete(currentPath);
        return new(creationTime, counter + 1, bytesToSend);
    }

    private static int SkipFiles(string imagePath, int counter, int skipCounter, string fileEnding)
    {
        var outOfSyncPath = Path.Combine(imagePath, (counter + skipCounter).ToString(CounterFormat) + fileEnding);
        if (Path.Exists(outOfSyncPath))
        {
            for (var i = 0; i < skipCounter; i++)
            {
                var deletePath = Path.Combine(imagePath, (counter + i).ToString(CounterFormat) + fileEnding);
                if (Path.Exists(deletePath)) File.Delete(deletePath);
            }
            counter += skipCounter;
        }

        return counter;
    }
}
