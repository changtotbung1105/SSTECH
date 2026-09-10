namespace PartnerIntegration.Domain.Transactions;

public sealed class DomainValidationException(string message) : Exception(message);
