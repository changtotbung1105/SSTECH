namespace PartnerIntegration.Application.Transactions;

public interface ITransactionPublisher
{
    // Complete only after the destination confirms acceptance; propagate failures/cancellation.
    Task PublishAsync(TransactionMessage message, CancellationToken cancellationToken);
}
