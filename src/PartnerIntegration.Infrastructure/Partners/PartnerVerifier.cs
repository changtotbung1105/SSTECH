using System.Net;
using System.Net.Http.Json;
using Polly.Timeout;
using PartnerIntegration.Application.Partners;
using PartnerIntegration.Application.Common;
namespace PartnerIntegration.Infrastructure.Partners;

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
