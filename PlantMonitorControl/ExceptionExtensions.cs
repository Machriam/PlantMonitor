using PlantMonitorControl;
using Serilog;

namespace PlantMonitorControl;

internal static class ExceptionExtensions
{
    public static void LogError(this Exception ex, string message = "") => Log.Logger.Error("{message}{error}\n{stacktrace}", message.IsEmpty() ? "" : $"{message}\n", ex.Message, ex.StackTrace);
}
