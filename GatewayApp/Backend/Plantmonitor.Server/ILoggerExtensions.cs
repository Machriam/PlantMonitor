using System.Runtime.CompilerServices;
using Plantmonitor.DataModel.DataModel;

namespace Plantmonitor.Server;

internal static class ILoggerExtensions
{
    public static void Log(this Serilog.ILogger logger, string message, PhotoTourEventType type)
    {
        switch (type)
        {
            case PhotoTourEventType.Debug:
                logger.Write(Serilog.Events.LogEventLevel.Debug, message);
                break;

            case PhotoTourEventType.Information:
                logger.Write(Serilog.Events.LogEventLevel.Information, message);
                break;

            case PhotoTourEventType.Warning:
                logger.Write(Serilog.Events.LogEventLevel.Warning, message);
                break;

            case PhotoTourEventType.Error:
                logger.Write(Serilog.Events.LogEventLevel.Error, message);
                break;

            case PhotoTourEventType.Critical:
                logger.Write(Serilog.Events.LogEventLevel.Fatal, message);
                break;
        }
    }
}
