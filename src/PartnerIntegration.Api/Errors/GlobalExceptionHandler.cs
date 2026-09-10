using Microsoft.AspNetCore.Diagnostics;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Domain.Transactions;

namespace PartnerIntegration.Api.Errors;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }
        var (status, title) = exception switch
        {
            DomainValidationException => (400, "Invalid transaction data."),
            TimeoutException => (504, "Partner verification timed out."),
            DependencyUnavailableException => (503, "A required service is temporarily unavailable."),
            _ => (500, "An unexpected error occurred.")
        };
        logger.LogError(exception, "Request failed with status {Status}; trace {TraceId}", status, context.TraceIdentifier);
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        return true;
    }
}
