using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PartnerIntegration.Application.Transactions;

namespace PartnerIntegration.Api.Transactions;

[ApiController]
[Authorize]
[Route("api/v1/partner/transactions")]
public sealed class TransactionsController(ISubmitTransaction submitTransaction) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(TransactionRequest request, CancellationToken cancellationToken)
    {
        var receipt = await submitTransaction.ExecuteAsync(new SubmitTransactionCommand(
            request.PartnerId!, request.TransactionReference!, request.Amount!.Value,
            request.Currency!, request.Timestamp!.Value), cancellationToken);
        if (receipt is null)
            return Problem(statusCode: 422, title: "Partner is not verified.");

        return Accepted(new { receipt.MessageId, receipt.TransactionReference, Status = "queued" });
    }
}
