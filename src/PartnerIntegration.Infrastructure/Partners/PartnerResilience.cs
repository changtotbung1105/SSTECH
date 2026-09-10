using Microsoft.Extensions.Http.Resilience;
using Polly;
namespace PartnerIntegration.Infrastructure.Partners;

public static class PartnerResilience
{
    public static void Configure(HttpStandardResilienceOptions options, TimeSpan? retryDelay = null)
    {
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = retryDelay ?? TimeSpan.FromMilliseconds(200);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = retryDelay is null;
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(20);
        options.CircuitBreaker.MinimumThroughput = 10;
    }
}
