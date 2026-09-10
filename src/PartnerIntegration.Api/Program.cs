using Microsoft.AspNetCore.Authentication;
using PartnerIntegration.Api.Security;
using PartnerIntegration.Api.Errors;
using PartnerIntegration.Api.Partners;
using PartnerIntegration.Application.Partners;
using PartnerIntegration.Application.Transactions;
using PartnerIntegration.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration["Security:ApiKey"]))
    throw new InvalidOperationException("Configure Security:ApiKey using environment variables or user secrets.");
builder.Services.AddControllers();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISubmitTransaction, SubmitTransaction>();
builder.Services.AddSingleton<IFailureSampler, RandomFailureSampler>();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
if (app.Environment.IsDevelopment())
{
    app.MapGet("/mock/partners/{partnerId}", (string partnerId, IFailureSampler sampler) =>
    {
        if (sampler.ShouldTimeout()) throw new TimeoutException("Simulated partner API timeout.");
        return Results.Ok(new PartnerDetails(partnerId, $"Demo partner {partnerId}", true));
    });
}
app.Run();

public partial class Program { }
