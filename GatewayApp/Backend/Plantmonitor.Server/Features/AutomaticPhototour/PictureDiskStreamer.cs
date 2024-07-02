using Microsoft.AspNetCore.SignalR.Client;
using Plantmonitor.Shared.Features.ImageStreaming;
using Plantmonitor.Server.Features.AppConfiguration;
using MessagePack.Formatters;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public interface IPictureDiskStreamer : IAsyncDisposable
{
    Task StartStreamingToDisc(string ip, string deviceId, CameraTypeInfo cameraType, StreamingMetaData data, Action<string> picturePathCallback,
            Func<CameraStreamFormatter, Task> imageReceivedCallback, CancellationToken token);

    bool StreamingFinished();
}

public class PictureDiskStreamer(IEnvironmentConfiguration configuration) : IPictureDiskStreamer
{
    private HubConnection? _connection;
    private bool _connectionClosed;

    public async Task StartStreamingToDisc(string ip, string deviceId, CameraTypeInfo cameraType, StreamingMetaData data, Action<string> picturePathCallback,
        Func<CameraStreamFormatter, Task> imageReceivedCallback, CancellationToken token)
    {
        var picturePath = configuration.PicturePath(deviceId);
        var sequenceId = DateTime.Now.ToString(CameraStreamFormatter.PictureDateFormat);
        var storagePath = Path.Combine(picturePath, sequenceId);
        if (!picturePath.IsEmpty()) Directory.CreateDirectory(storagePath);
        _connection = new HubConnectionBuilder()
            .WithUrl($"https://{ip}/hub/video")
            .WithAutomaticReconnect(new SignarRRetryPolicy1Second())
            .AddMessagePackProtocol()
            .Build();
        await _connection.StartAsync(token);
        _connection.Closed += Connection_Closed;
        picturePathCallback(storagePath);
        await StreamData(storagePath, cameraType, data, imageReceivedCallback, token);
    }

    private async Task Connection_Closed(Exception? arg)
    {
        _connectionClosed = true;
        await DisposeAsync();
    }

    public bool StreamingFinished()
    {
        return _connectionClosed;
    }

    private async Task StreamData(string path, CameraTypeInfo cameraInfo, StreamingMetaData data, Func<CameraStreamFormatter, Task> callback, CancellationToken token)
    {
        if (_connection == null) return;
        var stream = await _connection.StreamAsChannelAsync<byte[]>(cameraInfo.SignalRMethod, data, token);
        while (await stream.WaitToReadAsync(token))
        {
            await foreach (var image in stream.ReadAllAsync(token))
            {
                var cameraStream = CameraStreamFormatter.FromBytes(image);
                callback(cameraStream).RunInBackground(ex => ex.LogError());
                if (!path.IsEmpty() && cameraStream.PictureData != null)
                {
                    cameraStream.WriteToFile(path, cameraInfo);
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_connection == null) return ValueTask.CompletedTask;
        _connection.Closed -= Connection_Closed;
        return _connection.DisposeAsync();
    }
}
