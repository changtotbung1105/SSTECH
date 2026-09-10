using Moq;
using PartnerIntegration.Application.Common;
using PartnerIntegration.Application.Partners;
using PartnerIntegration.Application.Transactions;
using PartnerIntegration.Domain.Transactions;
using Xunit;

namespace PartnerIntegration.Tests;

public class UseCaseTests
{
    private static readonly SubmitTransactionCommand Valid = new("P-1001", "TXN-1", 250m, "USD",
        DateTimeOffset.Parse("2024-05-10T14:30:00Z"));
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    }

    [Fact]
    public async Task Receipt_is_returned_only_after_publishing_confirms()
    {
        var verifier = new Mock<IPartnerVerifier>();
        var publisher = new Mock<ITransactionPublisher>();
        var clock = new FixedClock();
        verifier.Setup(x => x.VerifyAsync("P-1001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerDetails("P-1001", "Verified name", true));
        var confirmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TransactionMessage? message = null;
        publisher.Setup(x => x.PublishAsync(It.IsAny<TransactionMessage>(), It.IsAny<CancellationToken>()))
            .Callback<TransactionMessage, CancellationToken>((value, _) => message = value)
            .Returns(confirmed.Task);
        var useCase = new SubmitTransaction(verifier.Object, publisher.Object, clock);

        var execution = useCase.ExecuteAsync(Valid, default);
        Assert.False(execution.IsCompleted);
        Assert.NotNull(message);
        Assert.Equal("Verified name", message.PartnerName);
        Assert.Equal(clock.GetUtcNow(), message.ReceivedAt);
        Assert.Equal(Valid.Timestamp, message.Timestamp);
        confirmed.SetResult();
        var receipt = await execution;
        Assert.Equal(message.MessageId, receipt!.MessageId);
        Assert.Equal(Valid.TransactionReference, receipt.TransactionReference);
    }

    [Theory]
    [InlineData("", "TXN-1", 1, "USD", false)]
    [InlineData("P-1", " ", 1, "USD", false)]
    [InlineData("P-1", "TXN-1", 0, "USD", false)]
    [InlineData("P-1", "TXN-1", -1, "USD", false)]
    [InlineData("P-1", "TXN-1", 1, "XYZ", false)]
    [InlineData("P-1", "TXN-1", 1, "USD", true)]
    public async Task Invalid_commands_cannot_bypass_domain_rules(string partnerId, string reference,
        int amount, string currency, bool defaultTimestamp)
    {
        var verifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<ITransactionPublisher>(MockBehavior.Strict);
        var useCase = new SubmitTransaction(verifier.Object, publisher.Object, new FixedClock());
        var command = new SubmitTransactionCommand(partnerId, reference, amount, currency,
            defaultTimestamp ? default : Valid.Timestamp);
        await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(command, default));
        verifier.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task Rejected_partner_never_publishes(bool missing)
    {
        var verifier = new Mock<IPartnerVerifier>();
        verifier.Setup(x => x.VerifyAsync(Valid.PartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(missing ? null : new PartnerDetails(Valid.PartnerId, "Partner", false));
        var publisher = new Mock<ITransactionPublisher>(MockBehavior.Strict);
        Assert.Null(await new SubmitTransaction(verifier.Object, publisher.Object, new FixedClock())
            .ExecuteAsync(Valid, default));
        publisher.VerifyNoOtherCalls();
    }

    [Theory][InlineData("other", "Partner")][InlineData("P-1001", " ")]
    public async Task Inconsistent_verification_cannot_be_published(string id, string name)
    {
        var verifier = new Mock<IPartnerVerifier>();
        verifier.Setup(x => x.VerifyAsync(Valid.PartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PartnerDetails(id, name, true));
        var publisher = new Mock<ITransactionPublisher>(MockBehavior.Strict);
        await Assert.ThrowsAsync<DependencyUnavailableException>(() =>
            new SubmitTransaction(verifier.Object, publisher.Object, new FixedClock()).ExecuteAsync(Valid, default));
        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancellation_is_forwarded_to_both_ports()
    {
        using var cancellation = new CancellationTokenSource();
        var verifier = new Mock<IPartnerVerifier>(MockBehavior.Strict);
        var publisher = new Mock<ITransactionPublisher>(MockBehavior.Strict);
        verifier.Setup(x => x.VerifyAsync(Valid.PartnerId, cancellation.Token))
            .ReturnsAsync(new PartnerDetails(Valid.PartnerId, "Partner", true));
        publisher.Setup(x => x.PublishAsync(It.IsAny<TransactionMessage>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SubmitTransaction(verifier.Object, publisher.Object, new FixedClock()).ExecuteAsync(Valid, cancellation.Token));
        verifier.VerifyAll();
        publisher.VerifyAll();
    }
}
