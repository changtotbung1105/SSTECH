using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;
using PartnerIntegration.Api.Transactions;

namespace PartnerIntegration.Api.Partners;

public sealed record PartnerDetails(string PartnerId, string Name, bool IsVerified);

public interface IPartnerVerifier
{
    Task<PartnerDetails?> VerifyAsync(string partnerId, CancellationToken cancellationToken);
}

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

public sealed class PartnerVerifier(HttpClient client) : IPartnerVerifier
{
    public async Task<PartnerDetails?> VerifyAsync(string partnerId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync($"mock/partners/{Uri.EscapeDataString(partnerId)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var partner = await response.Content.ReadFromJsonAsync<PartnerDetails>(cancellationToken);
            if (partner is null || partner.PartnerId != partnerId || string.IsNullOrWhiteSpace(partner.Name))
                throw new DependencyUnavailableException("Partner verification returned an invalid response.");
            return partner.IsVerified ? partner : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException
            or Polly.CircuitBreaker.BrokenCircuitException or System.Text.Json.JsonException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Partner verification is temporarily unavailable.", ex);
        }
    }
}

public interface IFailureSampler { bool ShouldTimeout(); }
public sealed class RandomFailureSampler : IFailureSampler
{
    public bool ShouldTimeout() => Random.Shared.NextDouble() < 0.30;
}
