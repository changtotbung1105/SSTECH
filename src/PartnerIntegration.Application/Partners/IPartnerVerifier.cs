namespace PartnerIntegration.Application.Partners;

public interface IPartnerVerifier
{
    // Null represents a missing/unverified partner; dependency failures must throw.
    Task<PartnerDetails?> VerifyAsync(string partnerId, CancellationToken cancellationToken);
}
