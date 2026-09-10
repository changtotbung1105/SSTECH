using PartnerIntegration.Application.Partners;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Domain.Transactions;

namespace PartnerIntegration.Application.Transactions;

public sealed class SubmitTransaction(IPartnerVerifier verifier, ITransactionPublisher publisher,
    TimeProvider clock) : ISubmitTransaction
{
    public async Task<SubmissionReceipt?> ExecuteAsync(SubmitTransactionCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transaction = new PartnerTransaction(command.PartnerId, command.TransactionReference,
            command.Amount, command.Currency, command.Timestamp);
        var partner = await verifier.VerifyAsync(transaction.PartnerId, cancellationToken);
        if (partner is null || !partner.IsVerified) return null;
        if (partner.PartnerId != transaction.PartnerId || string.IsNullOrWhiteSpace(partner.Name))
            throw new DependencyUnavailableException("Partner verification returned an invalid response.");

        var message = new TransactionMessage(Guid.NewGuid(), 1, transaction.PartnerId, partner.Name,
            transaction.TransactionReference, transaction.Amount, transaction.Currency,
            transaction.Timestamp, clock.GetUtcNow());
        await publisher.PublishAsync(message, cancellationToken);
        return new SubmissionReceipt(message.MessageId, message.TransactionReference);
    }
}
