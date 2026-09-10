namespace PartnerIntegration.Application.Common;

public sealed class DependencyUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
