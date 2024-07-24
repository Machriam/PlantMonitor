namespace Plantmonitor.Server.Features.ImageStitching;

public class VirtualImageWorker : IHostedService
{
    private Timer? _timer;
    private long? _photoTourToProcess;

    public void CancelCalculation()
    {
        _photoTourToProcess = null;
    }

    public void RecalculateTour(long photoTourId)
    {
        _photoTourToProcess = photoTourId;
    }

    public void CreateVirtualImage()
    {
        if (_photoTourToProcess == null) return;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => CreateVirtualImage(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
