using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PB.Email;
using PB.Monitoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Email.API.MessageQueue
{
    public class Subscriber
    {
        ILogger<Subscriber> _logger;
        readonly IMetrics _metrics;
        MQClient _client;
        IConfiguration _config;
        MessageQueueSettings _queueSettings;
        IServiceProvider _serviceProvider;
        Timer _unsentQueueTimer;
        Timer _reconnectTimer;
        readonly object _clientLock = new object();

        public Subscriber(ILogger<Subscriber> logger, IMetrics metrics, MessageQueueSettings queueSettings, IConfiguration config, IServiceProvider sp)
        {
            _logger = logger ?? throw new ArgumentNullException("ILogger is null");
            _metrics = metrics ?? throw new ArgumentNullException("IMetrics is null");
            _queueSettings = queueSettings ?? throw new ArgumentNullException("MessageQueueSettings is null");
            _config = config ?? throw new ArgumentNullException("IConfiguration is null");

            _serviceProvider = sp;

            _client = new MQClient(_queueSettings);
            _unsentQueueTimer = new Timer();
            _reconnectTimer = new Timer();
        }

        public void Start()
        {
            Subscribe();

            InitializeUnsentQueueCleaner(_logger);

            InitializeReconnectChecker(_logger);
        }

        private void Subscribe()
        {
            _client.Subscribe(_queueSettings.MainQueue, "EmailAPI", Action(_logger));
        }

        protected Action<MQMessage> Action(ILogger<Subscriber> _logger)
        {
            Action<MQMessage> messageHandler = (msg) => {
                try
                {
                    lock(_clientLock)
                    {
                        if (msg.Body == null)
                            _client.Drop(msg.DeliveryTag);

                        _logger.LogInformation($"Processing message from queue. MessageId: {msg.MessageId}");

                        var json = Encoding.UTF8.GetString(msg.Body);
                        var payload = JsonConvert.DeserializeObject<EmailPayloadConfig>(json);

                        //*********** Testing Ack ****************
                        //Task.Delay(TimeSpan.FromSeconds(10)).Wait();
                        //_client.Ack(msg.DeliveryTag);
                        //return;

                        var success = Process(payload);

                        if (success)
                        {
                            _logger.LogInformation($"Message acknowledged and processed. MessageId: {msg.MessageId}");
                            _client.Ack(msg.DeliveryTag);
                        }
                        else
                        {
                            if (DateTime.UtcNow < payload.EmailPayload.ExpiryUtc)
                            {
                                _logger.LogInformation($"Message Requeued due to failed processing. MessageId: {msg.MessageId}");
                                var task = Task.Run(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromMinutes(2));
                                    msg.Count++;

                                    _client.Requeue(msg.DeliveryTag);
                                });
                            }
                            else
                            {
                                _logger.LogInformation($"Message sent to 'Unsent' queue '{_queueSettings.UnsentQueue}' because of expiry or due to {msg.Count} failed send attempts. MessageId: {msg.MessageId}");

                                _client.Ack(msg.DeliveryTag);

                                msg.RoutingKey = _queueSettings.GetRoutingKey(_queueSettings.UnsentQueue);

                                var published = _client.Publish(_queueSettings.Exchange, msg);

                                if (!published)
                                {
                                    _logger.LogInformation($"Failed to send message (MessageId {msg.MessageId}) to 'Unsent' queue '{_queueSettings.UnsentQueue}'");
                                }
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "Error occured why processing MQ message: {msg.MessageId}");
                }
            };

            return messageHandler;
        }

        protected bool Process(EmailPayloadConfig emailPayloadConfig)
        {
            EmailManager _emailManager = _serviceProvider.CreateScope().ServiceProvider.GetService<EmailManager>();
            try
            {
                var response = _emailManager.SendEmail(emailPayloadConfig.EmailPayload, emailPayloadConfig.ResponsysEmailConfig).GetAwaiter().GetResult();

                if (response.Any(m => !m.EmailTriggered))
                {
                    if (response.All(m => !m.EmailTriggered))
                        _metrics.ServiceCheck("Email", ServiceStatus.WARNING, new string[] { _config["Env"] });

                    _logger.LogError($"Failed to email recipients: {string.Join(", ", response.Where(m => !m.EmailTriggered).Select(m => m.EmailAddress))}.");
                }

                if (response.All(m => m.EmailSentSuccess))
                {
                    _metrics.ServiceCheck("Email", ServiceStatus.OK, new string[] { _config["Env"] });
                    _logger.LogInformation("Email sent successully!");
                }
            }
            catch (Exception ex)
            {
                // If exception is Transient, message will be requeued for a retry. "IsTransientException" is inserted to Data in a Polly callback (see start.cs)
                if (ex.Data.Contains("IsTransientException"))
                {
                    return false;
                }

                _metrics.ServiceCheck("Email", ServiceStatus.CRITICAL, new string[] { _config["Env"] });
                _logger.LogError(ex, $"Email attempt not successful. Error not transient. {Environment.NewLine} {_emailManager.SanitizeMessageForLogging(JsonConvert.SerializeObject(emailPayloadConfig))}");
            }
            return true;
        }

        private void InitializeUnsentQueueCleaner(ILogger<Subscriber> _logger)
        {
            // Empty out the Unsent queue for messages that are 4+ days old

            _unsentQueueTimer.Interval = TimeSpan.FromMinutes(25).TotalMilliseconds;
            _unsentQueueTimer.Elapsed += (object o, ElapsedEventArgs args) =>
            {
                int hour = DateTime.Now.Hour;

                // Do this in the middle of the night to not crowd out client channel
                if (hour >= 3 && hour <= 4)
                {
                    try
                    {
                        var requeues = new List<MQMessage>();
                        MQMessage unsentMsg = null;

                        lock (_clientLock)
                        {
                            while ((unsentMsg = _client.GetMessage(_queueSettings.UnsentQueue)) != null)
                            {
                                if ((DateTime.UtcNow - unsentMsg.TimestampUtc).TotalDays >= 4)
                                {
                                    _client.Ack(unsentMsg.DeliveryTag);
                                    _logger.LogInformation($"Message {unsentMsg.MessageId}) removed from 'Unsent' queue '{_queueSettings.UnsentQueue}' due to expiry");
                                    // var json = Encoding.UTF8.GetString(unsentMsg.Body.ToArray());
                                }
                                else
                                {
                                    requeues.Add(unsentMsg);
                                }
                            }

                            foreach (var m in requeues)
                            {
                                _client.Requeue(m.DeliveryTag);
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while attempting to clean up 'Unsent Message' queue");
                    }
                }
            };
            _unsentQueueTimer.Enabled = true;
        }

        /// <summary>
        /// RabbitMQ automatically reconnects if there is an unexpected failure.
        /// In case the auto reconnect fails, handle it manually.
        /// </summary>
        private void InitializeReconnectChecker(ILogger<Subscriber> _logger)
        {
            _reconnectTimer.Interval = TimeSpan.FromMinutes(10).TotalMilliseconds;
            _reconnectTimer.Elapsed += (object o, ElapsedEventArgs args) =>
            {
                try
                {
                    lock (_clientLock)
                    {
                        if (!_client.IsAlive)
                        {
                            _logger.LogInformation("CONSUMER is not alive. Attempting to reconnect.");

                            _client.Dispose();
                            _client = new MQClient(_queueSettings);
                            Subscribe();
                        }
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, "CONSUMER RECONNECT failed due to exception.");
                }
            };
            _reconnectTimer.Enabled = true;
        }

        public void Dispose()
        {
            _unsentQueueTimer.Dispose();
            _reconnectTimer.Dispose();
            _client.Dispose();
        }
    }
}
