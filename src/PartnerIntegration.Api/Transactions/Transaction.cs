using System.ComponentModel.DataAnnotations;

namespace PartnerIntegration.Api.Transactions;

public sealed record TransactionRequest
{
    [Required, StringLength(100)] public string? PartnerId { get; init; }
    [Required, StringLength(100)] public string? TransactionReference { get; init; }
    [Required, PositiveAmount] public decimal? Amount { get; init; }
    [Required, SupportedCurrency] public string? Currency { get; init; }
    [Required, NonDefaultTimestamp] public DateTimeOffset? Timestamp { get; init; }
}

public sealed class PositiveAmountAttribute : ValidationAttribute
{
    public PositiveAmountAttribute() => ErrorMessage = "Amount must be greater than zero.";
    public override bool IsValid(object? value) => value is null || value is decimal amount && amount > 0;
}

public sealed class SupportedCurrencyAttribute : ValidationAttribute
{
    // Explicit business allow-list; extend when onboarding a new settlement currency.
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
        { "USD", "EUR", "GBP", "VND", "JPY", "SGD", "AUD", "CAD", "CHF", "CNY", "THB" };
    public SupportedCurrencyAttribute() => ErrorMessage = "Currency must be a supported uppercase ISO 4217 code.";
    public override bool IsValid(object? value) => value is null || value is string code && Supported.Contains(code);
}

public sealed class NonDefaultTimestampAttribute : ValidationAttribute
{
    public NonDefaultTimestampAttribute() => ErrorMessage = "Timestamp must be a non-default date and time.";
    public override bool IsValid(object? value) => value is null || value is DateTimeOffset timestamp && timestamp != default;
}

public sealed record TransactionMessage(Guid MessageId, int SchemaVersion, string PartnerId,
    string PartnerName, string TransactionReference, decimal Amount, string Currency,
    DateTimeOffset Timestamp, DateTimeOffset ReceivedAt);

public interface ITransactionPublisher
{
    Task PublishAsync(TransactionMessage message, CancellationToken cancellationToken);
}

public sealed class DependencyUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
