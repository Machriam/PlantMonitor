using Microsoft.AspNetCore.SignalR;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Globalization;
using System.Threading.Channels;

namespace PlantMonitorControl.Features.MeasureTemperature;

public class TemperatureHub(IClick2TempInterop clickInterop, ILogger<TemperatureHub> logger) : Hub
{
    private static Channel<TemperatureStreamData> CreateChannel()
    {
        return Channel.CreateBounded<TemperatureStreamData>(new BoundedChannelOptions(1)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
    }

    public async Task<ChannelReader<TemperatureStreamData>> StreamTemperatureData(string[] devices, CancellationToken token)
    {
        var path = clickInterop.StartTemperatureReading(devices);
        var channel = CreateChannel();
        _ = ReadImagesFromFiles(channel, path, token);
        await Task.Yield();
        return channel.Reader;
    }

    private async Task ReadImagesFromFiles(Channel<TemperatureStreamData> channel, string folder, CancellationToken token)
    {
        while (true)
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                await ReadFromFile(channel, file, token);
            }
            await Task.Delay(100, token);
        }
    }

    private async Task ReadFromFile(Channel<TemperatureStreamData> channel, string file, CancellationToken token)
    {
        try
        {
            if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(file)).TotalMilliseconds < 500) await Task.Delay(500, token);
            foreach (var line in File.ReadAllLines(file))
            {
                var split = line.Split(":");
                var device = split[0];
                var temperature = float.Parse(split[1].Trim(), CultureInfo.InvariantCulture);
                await channel.Writer.WriteAsync(new(temperature, device, File.GetCreationTimeUtc(file)), token);
            }
            File.Delete(file);
        }
        catch (Exception ex)
        {
            logger.LogError("Exception: {expection}\n{stacktrace}", ex.Message, ex.StackTrace);
        }
    }
}
