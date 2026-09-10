using System.ComponentModel.DataAnnotations;
using PartnerIntegration.Domain.Transactions;

namespace PartnerIntegration.Api.Transactions;

public sealed record TransactionRequest
{
    [Required, StringLength(TransactionRules.IdentifierMaxLength)] public string? PartnerId { get; init; }
    [Required, StringLength(TransactionRules.IdentifierMaxLength)] public string? TransactionReference { get; init; }
    [Required, PositiveAmount] public decimal? Amount { get; init; }
    [Required, SupportedCurrency] public string? Currency { get; init; }
    [Required, NonDefaultTimestamp] public DateTimeOffset? Timestamp { get; init; }
}

public sealed class PositiveAmountAttribute : ValidationAttribute
{
    public PositiveAmountAttribute() => ErrorMessage = "Amount must be greater than zero.";
    public override bool IsValid(object? value) => value is null || value is decimal amount && TransactionRules.IsPositiveAmount(amount);
}

public sealed class SupportedCurrencyAttribute : ValidationAttribute
{
    public SupportedCurrencyAttribute() => ErrorMessage = "Currency must be a supported uppercase ISO 4217 code.";
    public override bool IsValid(object? value) => value is null || value is string code && TransactionRules.IsSupportedCurrency(code);
}

public sealed class NonDefaultTimestampAttribute : ValidationAttribute
{
    public NonDefaultTimestampAttribute() => ErrorMessage = "Timestamp must be a non-default date and time.";
    public override bool IsValid(object? value) => value is null || value is DateTimeOffset timestamp && TransactionRules.IsValidTimestamp(timestamp);
}
