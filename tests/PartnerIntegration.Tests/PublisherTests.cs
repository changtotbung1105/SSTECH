using Microsoft.Extensions.Configuration;
using Moq;
using PartnerIntegration.Api.Infrastructure;
using PartnerIntegration.Api.Transactions;
using RabbitMQ.Client;
using Xunit;

namespace PartnerIntegration.Tests;

public class PublisherTests
{
    private static readonly TransactionMessage Message = new(Guid.NewGuid(), 1, "P-1001", "Partner",
        "TXN-1", 250m, "USD", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class Broker
    {
        public Mock<IConnectionFactory> Factory { get; } = new();
        public Mock<IConnection> Connection { get; } = new();
        public Mock<IChannel> Channel { get; } = new();
        public Broker()
        {
            Factory.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Connection.Object);
            Connection.SetupGet(x => x.IsOpen).Returns(true);
            Connection.Setup(x => x.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Channel.Object);
        }
        public RabbitMqPublisher Publisher() => new(new ConfigurationBuilder().Build(), Factory.Object);
    }

    [Fact]
    public async Task Publish_uses_durable_queue_persistent_message_and_mandatory_routing()
    {
        var broker = new Broker();
        await using var publisher = broker.Publisher();
        await publisher.PublishAsync(Message, default);
        broker.Channel.Verify(x => x.QueueDeclareAsync("partner.transactions", true, false, false,
            null, false, false, It.IsAny<CancellationToken>()), Times.Once);
        broker.Channel.Verify(x => x.BasicPublishAsync("", "partner.transactions", true,
            It.Is<BasicProperties>(p => p.Persistent && p.MessageId == Message.MessageId.ToString()
                && p.ContentType == "application/json"), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
        broker.Connection.Verify(x => x.CreateChannelAsync(
            It.Is<CreateChannelOptions>(o => o.PublisherConfirmationsEnabled && o.PublisherConfirmationTrackingEnabled),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reuses_connection_but_creates_separate_channels()
    {
        var broker = new Broker();
        await using var publisher = broker.Publisher();
        await publisher.PublishAsync(Message, default);
        await publisher.PublishAsync(Message, default);
        broker.Factory.Verify(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Once);
        broker.Connection.Verify(x => x.CreateChannelAsync(It.IsAny<CreateChannelOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Closed_connection_is_replaced()
    {
        var broker = new Broker();
        await using var publisher = broker.Publisher();
        await publisher.PublishAsync(Message, default);
        broker.Connection.SetupGet(x => x.IsOpen).Returns(false);
        await publisher.PublishAsync(Message, default);
        broker.Factory.Verify(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        broker.Connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Connection_failure_reports_unavailability()
    {
        var broker = new Broker();
        broker.Factory.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("offline"));
        await using var publisher = broker.Publisher();
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => publisher.PublishAsync(Message, default));
    }

    [Fact]
    public async Task Publish_waits_for_confirmation_and_reports_failure_without_retry()
    {
        var broker = new Broker();
        var confirmation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.Channel.Setup(x => x.BasicPublishAsync("", "partner.transactions", true,
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask(confirmation.Task));
        await using var publisher = broker.Publisher();
        var publishing = publisher.PublishAsync(Message, default);
        Assert.False(publishing.IsCompleted);
        confirmation.SetException(new IOException("confirmation lost"));
        await Assert.ThrowsAsync<DependencyUnavailableException>(() => publishing);
        broker.Channel.Verify(x => x.BasicPublishAsync("", "partner.transactions", true,
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Caller_cancellation_does_not_become_dependency_error()
    {
        var broker = new Broker();
        await using var publisher = broker.Publisher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(Message, cancellation.Token));
        broker.Factory.Verify(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
