using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Serilog;

namespace Plantmonitor.Server;

internal static class MatExtensions
{
    public static void LogCall(this Mat mat, Action<Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Logger.Debug($"OpenCv Call {memberName} in {sourceFilePath}:{sourceLineNumber}");
        Log.Logger.Debug($"Mat sizes: {mat.Width}x{mat.Height}");
        func(mat);
    }

    public static void LogCall(this Mat mat, Mat mat2, Action<Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Logger.Debug($"OpenCv Call {memberName} in {sourceFilePath}:{sourceLineNumber}");
        Log.Logger.Debug($"Mat sizes: {mat.Width}x{mat.Height}, {mat2.Width}x{mat2.Height}");
        func(mat, mat2);
    }

    public static void LogCall(this Mat mat, Mat mat2, Mat mat3, Action<Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        Log.Logger.Debug($"OpenCv Call {memberName} in {sourceFilePath}:{sourceLineNumber}");
        Log.Logger.Debug($"Mat sizes: {mat.Width}x{mat.Height}, {mat2.Width}x{mat2.Height}, {mat3.Width}x{mat3.Height}");
        func(mat, mat2, mat3);
    }

    public static byte[] BytesFromMat(this Mat mat)
    {
        var tempFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "temp.png");
        CvInvoke.Imwrite(tempFile, mat);
        return File.ReadAllBytes(tempFile);
    }
}
