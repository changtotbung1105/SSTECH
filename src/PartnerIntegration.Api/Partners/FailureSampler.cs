namespace PartnerIntegration.Api.Partners;

public interface IFailureSampler { bool ShouldTimeout(); }
public sealed class RandomFailureSampler : IFailureSampler
{
    public bool ShouldTimeout() => Random.Shared.NextDouble() < 0.30;
}
