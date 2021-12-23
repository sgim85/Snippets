using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PB.Monitoring;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Email.API.MessageQueue
{
    /// <summary>
    /// Background service that registers the MessageQueue producer and consumer for the email api.
    /// Send-Email requests will be queued by the producer to be consumed by the consumer. 
    /// This MessageQueue functionality can be disabled in the config.
    /// </summary>
    public class MQBackgroundService : BackgroundService
    {
        ILogger<MQBackgroundService> _logger;
        Subscriber _subscriber;
        Publisher _publisher;
        MessageQueueSettings _msgQueueSettings;

        public MQBackgroundService(ILogger<MQBackgroundService> logger, IMetrics metrics, Subscriber subscriber, Publisher publisher, MessageQueueSettings msgQueueSettings)
        {
            _logger = logger ?? throw new ArgumentNullException("ILogger is null");
            _subscriber = subscriber ?? throw new ArgumentNullException("EventBusConsumer is null");
            _publisher = publisher ?? throw new ArgumentNullException("Publisher is null");
            _msgQueueSettings = msgQueueSettings ?? throw new ArgumentNullException("MessageQueueSettings is null");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_msgQueueSettings.EnableMessageQueue)
                return Task.CompletedTask;

            _logger.LogInformation("EventBus background service is starting");

            stoppingToken.Register(() => _logger.LogDebug($"EventBus background service is stopping."));

            _subscriber.Start();

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Closing Event bus client.");
            _subscriber.Dispose();
            _publisher.Dispose();

            return Task.CompletedTask;
        }
    }
}
