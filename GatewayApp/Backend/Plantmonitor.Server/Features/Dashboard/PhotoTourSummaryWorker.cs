using System.Collections.Concurrent;
using System.IO.Compression;
using Emgu.CV;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoTourSummaryWorker(IEnvironmentConfiguration configuration, IServiceScopeFactory scopeFactory) : IHostedService
{
    private Timer? _processImageTimer;
    private Timer? _processFindImagesToProcessTimer;
    private static readonly ConcurrentDictionary<string, DateTime> s_imagesToProcess = new();
    private static readonly object s_lock = new();
    private static bool s_isProcessing;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processImageTimer = new Timer(_ => FindNextImageToProcess(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        _processFindImagesToProcessTimer = new Timer(_ => FindImagesToProcess(), default,
            (int)TimeSpan.FromSeconds(20).TotalMilliseconds, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processImageTimer == null || _processFindImagesToProcessTimer == null) return;
        await _processImageTimer.DisposeAsync();
        await _processFindImagesToProcessTimer.DisposeAsync();
    }

    public void FindImagesToProcess()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var existingResults = context.VirtualImageSummaries
            .Select(vis => new { vis.VirtualImageCreationDate, vis.VirtualImagePath })
            .ToList()
            .Select(x => (x.VirtualImageCreationDate, x.VirtualImagePath))
            .ToHashSet();
        foreach (var folder in configuration.VirtualImageFolders().OrderBy(f => f))
        {
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(f => f).Select(f => new FileInfo(f)))
            {
                if (existingResults.Contains((file.CreationTime, file.FullName))) continue;
                s_imagesToProcess.TryAdd(file.FullName, file.CreationTimeUtc);
            }
        }
    }

    public void FindNextImageToProcess()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        KeyValuePair<string, DateTime> nextImage;
        lock (s_lock)
        {
            if (s_isProcessing) return;
            if (s_imagesToProcess.IsEmpty) return;
            nextImage = s_imagesToProcess.First();
            s_isProcessing = true;
        }
        Action action = () => ProcessImage(nextImage.Key);
        action.Try(ex =>
        {
            context.VirtualImageSummaries.Add(new VirtualImageSummary
            {
                VirtualImageCreationDate = nextImage.Value,
                VirtualImagePath = nextImage.Key
            });
            ex.LogError();
        });
        lock (s_lock)
        {
            s_imagesToProcess.TryRemove(nextImage.Key, out _);
            s_isProcessing = false;
        }
    }

    public void ProcessImage(string image)
    {
        var tempFolder = Directory.CreateTempSubdirectory().FullName;
        var zip = new ZipArchive(File.OpenRead(image));
        var files = new HashSet<string>();
        foreach (var entry in zip.Entries)
        {
            var path = Path.Combine(tempFolder, entry.Name);
            File.WriteAllBytes(path, entry.Open().ConvertToArray());
            files.Add(path);
        }
        zip.Dispose();
        var visMat = CvInvoke.Imread(files.First(f => f.StartsWith(PhotoTourTrip.VisPrefix)));
        var rawIrMat = CvInvoke.Imread(files.First(f => f.StartsWith(PhotoTourTrip.RawIrPrefix)));
    }
}
