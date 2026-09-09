using System.Text.Json;
using PartnerIntegration.Api.Transactions;
using RabbitMQ.Client;

namespace PartnerIntegration.Api.Infrastructure;

public sealed class RabbitMqPublisher(IConfiguration configuration, IConnectionFactory factory) : ITransactionPublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private IConnection? connection;

    public async Task PublishAsync(TransactionMessage message, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var broker = await GetConnectionAsync(deadline.Token);
            // Channels are not shared between concurrent requests. Await broker confirms.
            await using var channel = await broker.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true), deadline.Token);
            var queue = configuration["RabbitMq:Queue"] ?? "partner.transactions";
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: deadline.Token);
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = "application/json",
                MessageId = message.MessageId.ToString(), Type = "partner.transaction.v1"
            };
            await channel.BasicPublishAsync("", queue, mandatory: true, properties,
                JsonSerializer.SerializeToUtf8Bytes(message, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                deadline.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException("Message broker is temporarily unavailable.", ex);
        }
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (connection is { IsOpen: true }) return connection;
            if (connection is not null) await connection.DisposeAsync();
            connection = await factory.CreateConnectionAsync(cancellationToken);
            return connection;
        }
        finally { connectionLock.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        connectionLock.Dispose();
    }
}
