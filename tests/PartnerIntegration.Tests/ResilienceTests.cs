using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PartnerIntegration.Api.Partners;
using PartnerIntegration.Api.Transactions;
using Xunit;

namespace PartnerIntegration.Tests;

public class ResilienceTests
{
    private sealed class StubHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(++Calls, cancellationToken);
    }

    private static ServiceProvider Create(StubHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient<IPartnerVerifier, PartnerVerifier>(client =>
        {
            client.BaseAddress = new Uri("http://partner/");
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(() => handler)
          .AddStandardResilienceHandler(options => PartnerResilience.Configure(options, TimeSpan.Zero));
        return services.BuildServiceProvider();
    }
    private static HttpResponseMessage Success() => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new PartnerDetails("P-1001", "Partner One", true)) };

    [Theory]
    [InlineData(408)][InlineData(429)][InlineData(500)][InlineData(502)][InlineData(503)][InlineData(504)]
    public async Task Transient_failure_is_retried_then_succeeds(int status)
    {
        var handler = new StubHandler((attempt, _) => Task.FromResult(attempt < 3 ? new HttpResponseMessage((HttpStatusCode)status) : Success()));
        using var services = Create(handler);
        var result = await services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default);
        Assert.Equal("Partner One", result!.Name);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Exhausted_retries_report_dependency_failure()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.GatewayTimeout)));
        using var services = Create(handler);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Network_failure_is_retried()
    {
        var handler = new StubHandler((attempt, _) => attempt == 1 ? throw new HttpRequestException("network") : Task.FromResult(Success()));
        using var services = Create(handler);
        Assert.NotNull(await services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Actual_attempt_timeout_is_retried()
    {
        var handler = new StubHandler(async (attempt, ct) =>
        {
            if (attempt == 1) await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Success();
        });
        using var services = Create(handler);
        Assert.NotNull(await services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(2, handler.Calls);
    }

    [Theory][InlineData(400)][InlineData(401)][InlineData(403)]
    public async Task Permanent_http_errors_are_not_retried(int status)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        using var services = Create(handler);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Unknown_partner_is_not_retried()
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var services = Create(handler);
        Assert.Null(await services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory][InlineData("null")][InlineData("invalid json")]
    [InlineData("{\"partnerId\":\"wrong\",\"name\":\"Partner\",\"isVerified\":true}")]
    public async Task Invalid_partner_response_is_rejected(string json)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }));
        using var services = Create(handler);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated_without_retry()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubHandler(async (_, ct) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Success();
        });
        using var services = Create(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<IPartnerVerifier>().VerifyAsync("P-1001", cancellation.Token));
        Assert.Equal(1, handler.Calls);
    }
}
