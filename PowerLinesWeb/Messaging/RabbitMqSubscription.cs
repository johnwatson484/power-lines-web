using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PowerLinesWeb.Messaging;

public sealed class RabbitMqSubscription : IDisposable
{
    readonly IModel channel;
    readonly string queue;
    readonly Func<string, CancellationToken, Task> process;
    readonly ILogger logger;
    readonly TimeSpan retryDelay;
    readonly CancellationToken stoppingToken;
    readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Stopped => stopped.Task;

    public RabbitMqSubscription(IConnection connection, string exchange, string queue,
        Func<string, CancellationToken, Task> process, ILogger logger, TimeSpan retryDelay, CancellationToken stoppingToken)
    {
        this.queue = queue;
        this.process = process;
        this.logger = logger;
        this.retryDelay = retryDelay;
        this.stoppingToken = stoppingToken;
        channel = connection.CreateModel();
        try
        {
            channel.ModelShutdown += (_, _) => stopped.TrySetResult();
            channel.CallbackException += (_, args) => RequestReconnect(args.Exception);
            // Keep the existing durable fanout exchanges and subscription queues compatible.
            channel.ExchangeDeclare(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
            channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
            channel.QueueBind(queue, exchange, "");
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += Receive;
            consumer.ConsumerCancelled += (_, _) =>
            {
                stopped.TrySetResult();
                return Task.CompletedTask;
            };
            channel.BasicConsume(queue, autoAck: false, consumer);
        }
        catch
        {
            channel.Dispose();
            throw;
        }
    }

    async Task Receive(object sender, BasicDeliverEventArgs delivery)
    {
        if (stoppingToken.IsCancellationRequested) return;
        try
        {
            // The client owns Body; decode before returning from this callback.
            await process(Encoding.UTF8.GetString(delivery.Body.Span), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save message from {Queue}, delivery {DeliveryTag}, redelivered={Redelivered}; requeueing after {Delay}",
                queue, delivery.DeliveryTag, delivery.Redelivered, retryDelay);
            try
            {
                // Keep failed messages, including invalid payloads, without a hot retry loop.
                await Task.Delay(retryDelay, stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                    channel.BasicNack(delivery.DeliveryTag, multiple: false, requeue: true);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception nackError) { RequestReconnect(nackError); }
            return;
        }

        if (stoppingToken.IsCancellationRequested) return;
        try { channel.BasicAck(delivery.DeliveryTag, multiple: false); }
        catch (Exception ex) { RequestReconnect(ex); }
    }

    void RequestReconnect(Exception error)
    {
        logger.LogError(error, "RabbitMQ subscription {Queue} failed; unacknowledged deliveries will be redelivered after reconnect", queue);
        stopped.TrySetResult();
    }

    public void Dispose()
    {
        // Closing the channel requeues deliveries whose handlers have not acknowledged them.
        try { channel.Abort(); }
        finally { channel.Dispose(); }
    }
}
