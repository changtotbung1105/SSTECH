using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PartnerIntegration.Api.Partners;
using PartnerIntegration.Api.Transactions;
using Xunit;

namespace PartnerIntegration.Tests;

public class EndpointTests
{
    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public FakeVerifier Verifier { get; } = new();
        public FakePublisher Publisher { get; } = new();
        public bool Timeout { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Security:ApiKey", "test-key");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPartnerVerifier>();
                services.RemoveAll<ITransactionPublisher>();
                services.RemoveAll<IFailureSampler>();
                services.AddSingleton<IPartnerVerifier>(Verifier);
                services.AddSingleton<ITransactionPublisher>(Publisher);
                services.AddSingleton<IFailureSampler>(new Sampler(() => Timeout));
            });
        }
        public HttpClient Client(bool authenticate = true)
        {
            var client = CreateClient();
            if (authenticate) client.DefaultRequestHeaders.Add("X-Api-Key", "test-key");
            return client;
        }
    }
    private sealed class Sampler(Func<bool> timeout) : IFailureSampler { public bool ShouldTimeout() => timeout(); }
    private sealed class FakeVerifier : IPartnerVerifier
    {
        public bool Verified { get; set; } = true;
        public bool Fail { get; set; }
        public int Calls { get; private set; }
        public Task<PartnerDetails?> VerifyAsync(string partnerId, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fail) throw new DependencyUnavailableException("failure");
            return Task.FromResult(Verified ? new PartnerDetails(partnerId, "Enriched partner", true) : null);
        }
    }
    private sealed class FakePublisher : ITransactionPublisher
    {
        public TransactionMessage? Message { get; private set; }
        public bool Fail { get; set; }
        public Task PublishAsync(TransactionMessage message, CancellationToken cancellationToken)
        {
            if (Fail) throw new DependencyUnavailableException("broker failure");
            Message = message;
            return Task.CompletedTask;
        }
    }
    private const string Endpoint = "/api/v1/partner/transactions";

    [Fact]
    public async Task Valid_transaction_is_enriched_and_queued()
    {
        await using var app = new ApiFactory();
        using var client = app.Client();
        var response = await client.PostAsJsonAsync(Endpoint, ValidationTests.Valid);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Enriched partner", app.Publisher.Message!.PartnerName);
        Assert.Equal(250m, app.Publisher.Message.Amount);
        Assert.Equal(1, app.Publisher.Message.SchemaVersion);
    }

    [Theory][InlineData("{}")][InlineData("null")][InlineData("{")]
    [InlineData("{\"timestamp\":\"not-a-date\"}")]
    public async Task Invalid_payload_returns_400_without_external_calls(string json)
    {
        await using var app = new ApiFactory();
        using var client = app.Client();
        var response = await client.PostAsync(Endpoint, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(0, app.Verifier.Calls);
        Assert.Null(app.Publisher.Message);
    }

    [Theory][InlineData(null)][InlineData("wrong-key")]
    public async Task Missing_or_invalid_key_returns_401(string? key)
    {
        await using var app = new ApiFactory();
        using var client = app.Client(false);
        if (key is not null) client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var response = await client.PostAsJsonAsync(Endpoint, ValidationTests.Valid);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(0, app.Verifier.Calls);
    }

    [Fact]
    public async Task Unverified_partner_is_not_queued()
    {
        await using var app = new ApiFactory();
        app.Verifier.Verified = false;
        using var client = app.Client();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Endpoint, ValidationTests.Valid)).StatusCode);
        Assert.Null(app.Publisher.Message);
    }

    [Theory][InlineData(true)][InlineData(false)]
    public async Task Dependency_failure_returns_503_without_accepting(bool partnerFailure)
    {
        await using var app = new ApiFactory();
        app.Verifier.Fail = partnerFailure;
        app.Publisher.Fail = !partnerFailure;
        using var client = app.Client();
        var response = await client.PostAsJsonAsync(Endpoint, ValidationTests.Valid);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Null(app.Publisher.Message);
        Assert.Contains("traceId", await response.Content.ReadAsStringAsync());
    }

    [Theory][InlineData(true, 504)][InlineData(false, 200)]
    public async Task Mock_timeout_is_translated_to_http(bool timeout, int expected)
    {
        await using var app = new ApiFactory { Timeout = timeout };
        using var client = app.Client(false);
        Assert.Equal(expected, (int)(await client.GetAsync("/mock/partners/P-1001")).StatusCode);
    }
}
