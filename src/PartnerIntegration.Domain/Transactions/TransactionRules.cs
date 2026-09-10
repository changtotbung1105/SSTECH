namespace PartnerIntegration.Domain.Transactions;

public static class TransactionRules
{
    public const int IdentifierMaxLength = 100;
    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.Ordinal)
        { "USD", "EUR", "GBP", "VND", "JPY", "SGD", "AUD", "CAD", "CHF", "CNY", "THB" };

    public static bool IsValidIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= IdentifierMaxLength;
    public static bool IsPositiveAmount(decimal amount) => amount > 0;
    public static bool IsSupportedCurrency(string? currency) =>
        currency is not null && SupportedCurrencies.Contains(currency);
    public static bool IsValidTimestamp(DateTimeOffset timestamp) => timestamp != default;
}
