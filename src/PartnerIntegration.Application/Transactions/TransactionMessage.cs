namespace PartnerIntegration.Application.Transactions;

public sealed record TransactionMessage(Guid MessageId, int SchemaVersion, string PartnerId,
    string PartnerName, string TransactionReference, decimal Amount, string Currency,
    DateTimeOffset Timestamp, DateTimeOffset ReceivedAt);
