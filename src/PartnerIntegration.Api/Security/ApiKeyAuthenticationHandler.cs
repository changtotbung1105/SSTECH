using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PartnerIntegration.Api.Security;

public sealed class ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var keys) || keys.Count != 1)
            return Task.FromResult(AuthenticateResult.NoResult());
        var expected = configuration["Security:ApiKey"]!;
        var matches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(keys[0] ?? "")));
        if (!matches) return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "partner-client") }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "ApiKey";
        await Results.Problem(statusCode: 401, title: "A valid X-Api-Key header is required.").ExecuteAsync(Context);
    }
}
