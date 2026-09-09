using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerIntegration.Api.Partners;

namespace PartnerIntegration.Api.Transactions;

[ApiController]
[Authorize]
[Route("api/v1/partner/transactions")]
public sealed class TransactionsController(IPartnerVerifier verifier, ITransactionPublisher publisher,
    TimeProvider clock) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(TransactionRequest request, CancellationToken cancellationToken)
    {
        var partner = await verifier.VerifyAsync(request.PartnerId!, cancellationToken);
        if (partner is null)
            return Problem(statusCode: 422, title: "Partner is not verified.");

        var message = new TransactionMessage(Guid.NewGuid(), 1, request.PartnerId!, partner.Name,
            request.TransactionReference!, request.Amount!.Value, request.Currency!,
            request.Timestamp!.Value, clock.GetUtcNow());
        await publisher.PublishAsync(message, cancellationToken);
        return Accepted(new { message.MessageId, message.TransactionReference, Status = "queued" });
    }
}
