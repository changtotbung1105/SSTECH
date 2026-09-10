namespace PartnerIntegration.Application.Transactions;

public interface ISubmitTransaction
{
    // Null means partner rejection. A receipt means publishing has been confirmed.
    Task<SubmissionReceipt?> ExecuteAsync(SubmitTransactionCommand command, CancellationToken cancellationToken);
}
