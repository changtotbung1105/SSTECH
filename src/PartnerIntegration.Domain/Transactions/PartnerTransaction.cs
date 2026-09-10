namespace PartnerIntegration.Domain.Transactions;

// Immutable business data. Invariants hold even when the use case is called without HTTP.
public sealed class PartnerTransaction
{
    public string PartnerId { get; }
    public string TransactionReference { get; }
    public decimal Amount { get; }
    public string Currency { get; }
    public DateTimeOffset Timestamp { get; }

    public PartnerTransaction(string partnerId, string transactionReference, decimal amount,
        string currency, DateTimeOffset timestamp)
    {
        if (!TransactionRules.IsValidIdentifier(partnerId))
            throw new DomainValidationException("PartnerId is required and must not exceed 100 characters.");
        if (!TransactionRules.IsValidIdentifier(transactionReference))
            throw new DomainValidationException("TransactionReference is required and must not exceed 100 characters.");
        if (!TransactionRules.IsPositiveAmount(amount))
            throw new DomainValidationException("Amount must be greater than zero.");
        if (!TransactionRules.IsSupportedCurrency(currency))
            throw new DomainValidationException("Currency must be a supported uppercase ISO 4217 code.");
        if (!TransactionRules.IsValidTimestamp(timestamp))
            throw new DomainValidationException("Timestamp must be a non-default date and time.");

        PartnerId = partnerId;
        TransactionReference = transactionReference;
        Amount = amount;
        Currency = currency;
        Timestamp = timestamp;
    }
}
