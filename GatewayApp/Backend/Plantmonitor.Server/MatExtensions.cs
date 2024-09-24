using System.Runtime.CompilerServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Serilog;

namespace Plantmonitor.Server;

public class ManagedMat(Mat mat) : IMat
{
    private bool _disposed = false;
    private Guid _guid = Guid.NewGuid();
    private Mat _mat = mat;
    public void Dispose()
    {
        _disposed = true;
        _mat.Dispose();
    }
    private void LogCall(string memberName, string sourceFilePath, int sourceLineNumber, params Mat[] mats)
    {
        //var sizes = mats.Select(m => $"{m.Width}x{m.Height}").Concat(", ");
        //var pointer = mats.Select(m => m.DataPointer).Concat(", ");
        Log.Logger.Debug($"OpenCv Call {memberName} in {sourceFilePath}:{sourceLineNumber}");
        //Log.Logger.Debug($"Mat sizes: {sizes}, Pointer: {pointer}");
    }

    public T LogCall<T>(this Mat mat, Func<Mat, T> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, mat);
        return func(mat);
    }

    public void LogCall(this Mat mat, Action<Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, mat);
        func(mat);
    }

    public void LogCall(this Mat mat, Mat mat2, Action<Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, mat, mat2);
        func(mat, mat2);
    }

    public void LogCall(IMat mat2, IMat mat3, Action<Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, mat, mat2, mat3);
        func(mat, mat2, mat3);
    }

    public void LogCall(this IEnumerable<Mat> mats, Mat mat, Action<IEnumerable<Mat>, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, [.. mats, mat]);
        func(mats, mat);
    }

    public void LogCall(IMat mat2, IMat mat3, IMat mat4, Action<Mat, Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, mat, mat2, mat3, mat4);
        func(_mat, ((ManagedMat)mat2)._mat, ((ManagedMat)mat3)._mat, ((ManagedMat)mat4)._mat);
    }


}
public interface IMat : IDisposable
{

}
internal static class MatExtensions
{
    public static IMat ToManagedMat(this Mat mat)
    {
        return new ManagedMat(mat);
    }
    public static byte[] BytesFromMat(this Mat mat)
    {
        var tempFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "temp.png");
        mat.LogCall(x => CvInvoke.Imwrite(tempFile, x));
        return File.ReadAllBytes(tempFile);
    }
}
