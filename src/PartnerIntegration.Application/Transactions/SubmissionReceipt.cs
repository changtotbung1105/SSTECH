namespace PartnerIntegration.Application.Transactions;

public sealed record SubmissionReceipt(Guid MessageId, string TransactionReference);
