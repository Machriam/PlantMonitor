namespace Plantmonitor.Shared.Extensions;

public static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var contentLength = response.Content.Headers.ContentLength;
        var inverseContentLength = 1d / (contentLength == 0 ? 1 : contentLength) ?? 1d;
        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (progress == null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken);
            return;
        }
        var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)(totalBytes * inverseContentLength)));
        await download.CopyToAsync(destination, 1024 * 1024 * 4, relativeProgress, cancellationToken);
        progress.Report(1);
    }
}

public static class StreamExtensions
{
    public static byte[] ConvertToArray(this Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream.ToArray();
    }

    public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!source.CanRead) throw new ArgumentException("Has to be readable", nameof(source));
        if (!destination.CanWrite) throw new ArgumentException("Has to be writable", nameof(destination));
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }
}
