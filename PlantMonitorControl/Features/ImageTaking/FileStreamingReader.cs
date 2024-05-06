namespace PlantMonitorControl.Features.MotorMovement;

public interface IFileStreamingReader
{
    Task<FileInfo> ReadNextFile(string imagePath, int counter, CancellationToken token);

    Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, CancellationToken token);
}

public record struct FileInfo(DateTime CreationDate, int NewCounter, byte[]? FileData);

public class FileStreamingReader : IFileStreamingReader
{
    public const string CounterFormat = "000000";

    public async Task<FileInfo> ReadNextFileWithSkipping(string imagePath, int counter, int howManyMoreRecentImagesMayExist, CancellationToken token)
    {
        counter = SkipFiles(imagePath, counter, howManyMoreRecentImagesMayExist);
        return await ReadNextFile(imagePath, counter, token);
    }

    public async Task<FileInfo> ReadNextFile(string imagePath, int counter, CancellationToken token)
    {
        var currentPath = Path.Combine(imagePath, $"{counter.ToString(CounterFormat)}.jpg");
        var nextPath = Path.Combine(imagePath, $"{(counter + 1).ToString(CounterFormat)}.jpg");
        if (!Path.Exists(nextPath)) return new(default, counter, default);
        var bytesToSend = await File.ReadAllBytesAsync(currentPath, token);
        var creationTime = File.GetCreationTimeUtc(currentPath);
        File.Delete(currentPath);
        return new(creationTime, counter + 1, bytesToSend);
    }

    private static int SkipFiles(string imagePath, int counter, int skipCounter)
    {
        var outOfSyncPath = Path.Combine(imagePath, $"{(counter + skipCounter).ToString(CounterFormat)}.jpg");
        if (Path.Exists(outOfSyncPath))
        {
            for (var i = 0; i < skipCounter; i++)
            {
                var deletePath = Path.Combine(imagePath, $"{(counter + i).ToString(CounterFormat)}.jpg");
                if (Path.Exists(deletePath)) File.Delete(deletePath);
            }
            counter += skipCounter;
        }

        return counter;
    }
}
