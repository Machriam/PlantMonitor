namespace Plantmonitor.Shared.Extensions;

public static class StreamExtensions
{
    public static byte[] ConvertToArray(this Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return memoryStream.ToArray();
    }
}
