using Microsoft.AspNetCore.SignalR.Client;

namespace Plantmonitor.Server.Features.TemperatureMonitor
{
    public class RetryPolicy1Second : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(1);
        }
    }
}
