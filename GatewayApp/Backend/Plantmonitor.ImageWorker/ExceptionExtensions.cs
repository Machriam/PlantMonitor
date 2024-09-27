using Serilog;

namespace Plantmonitor.ImageWorker;

internal static class ExceptionExtensions
{
    public static void LogError(this Exception ex) => Log.Logger.Error("{error}\n{stacktrace}", ex.Message, ex.StackTrace);
}
