using Emgu.CV;

namespace Plantmonitor.Server;

internal static class MatExtensions
{
    public static byte[] BytesFromMat(this Mat mat)
    {
        var tempFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "temp.png");
        CvInvoke.Imwrite(tempFile, mat);
        return File.ReadAllBytes(tempFile);
    }
}
