namespace PartnerIntegration.Application.Transactions;

public sealed record SubmitTransactionCommand(string PartnerId, string TransactionReference,
    decimal Amount, string Currency, DateTimeOffset Timestamp);
