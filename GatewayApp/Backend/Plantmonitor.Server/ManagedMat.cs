using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Emgu.CV;
using Serilog;

namespace Plantmonitor.Server;

public interface IManagedMat : IDisposable
{
    byte[] BytesFromMat();

    public bool IsDisposed { get; }

    void Execute(Action<Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

    void Execute(IEnumerable<IManagedMat> mats, Action<Mat, IEnumerable<Mat>> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

    void Execute(IManagedMat mat2, Action<Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

    void Execute(IManagedMat mat2, IManagedMat mat3, Action<Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

    void Execute(IManagedMat mat2, IManagedMat mat3, IManagedMat mat4, Action<Mat, Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);

    T Execute<T>(Func<Mat, T> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0);
}

public class ManagedMat : IManagedMat
{
    public ManagedMat(Mat mat) => _mat = mat;

    private bool _disposed = false;
    private readonly Guid _guid = Guid.NewGuid();
    private readonly Mat _mat;

    private static Mat GetMat(IManagedMat mat) => ((ManagedMat)mat)._mat;

    public bool IsDisposed => _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        Log.Logger.Information($"Disposing Mat {_guid}");
        _disposed = true;
        _mat.Dispose();
    }

    private void LogCall(string memberName, string sourceFilePath, int sourceLineNumber, params IManagedMat[] mats)
    {
        static string GetSize(Mat mat, bool disposed) => disposed ? "Disposed" : $"{mat.Width}x{mat.Height}";
        var matObjects = mats.Select(m => new { Disposed = ((ManagedMat)m)._disposed, Guid = ((ManagedMat)m)._guid, Mat = m.Pipe(GetMat) });
        var matInfos = matObjects.Select(m => $"Mat {m.Guid}: {GetSize(m.Mat, m.Disposed)}").Concat(", ");
        Log.Logger.Information($"OpenCv Call {memberName} in {sourceFilePath}:{sourceLineNumber}");
        Log.Logger.Information($"{matInfos}");
        if (matObjects.Any(m => m.Disposed)) throw new Exception("Trying to access disposed Mat");
    }

    public T Execute<T>(Func<Mat, T> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, this);
        return func(_mat);
    }

    public void Execute(Action<Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, this);
        func(_mat);
    }

    public void Execute(IManagedMat mat2, Action<Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, this, mat2);
        func(_mat, mat2.Pipe(GetMat));
    }

    public void Execute(IManagedMat mat2, IManagedMat mat3, Action<Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, this, mat2, mat3);
        func(_mat, mat2.Pipe(GetMat), mat3.Pipe(GetMat));
    }

    public void Execute(IEnumerable<IManagedMat> mats, Action<Mat, IEnumerable<Mat>> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, [.. mats, this]);
        func(_mat, mats.Select(m => m.Pipe(GetMat)));
    }

    public void Execute(IManagedMat mat2, IManagedMat mat3, IManagedMat mat4, Action<Mat, Mat, Mat, Mat> func, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
    {
        LogCall(memberName, sourceFilePath, sourceLineNumber, this, mat2, mat3, mat4);
        func(_mat, mat2.Pipe(GetMat), mat3.Pipe(GetMat), mat4.Pipe(GetMat));
    }

    public byte[] BytesFromMat()
    {
        var tempFile = Path.Combine(Directory.CreateTempSubdirectory().FullName, "temp.png");
        Execute(x => CvInvoke.Imwrite(tempFile, x));
        return File.ReadAllBytes(tempFile);
    }
}
