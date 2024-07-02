using Microsoft.AspNetCore.SignalR.Client;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public class SignarRRetryPolicy1Second : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            return TimeSpan.FromSeconds(1);
        }
    }
}
