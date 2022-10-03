using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
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
    public class Subscriber : IDisposable
    {
        ILogger<Subscriber> _logger;
        readonly IMetrics _metrics;
        MQClient _client;
        IConfiguration _config;
        MessageQueueSettings _queueSettings;
        IServiceProvider _serviceProvider;
        Timer _unsentQueueTimer;
        Timer _reconnectTimer;
        IMemoryCache _memoryCache;
        readonly object _clientLock = new object();

        public Subscriber(ILogger<Subscriber> logger, IMetrics metrics, MessageQueueSettings queueSettings, IConfiguration config, IServiceProvider sp)
        {
            _logger = logger ?? throw new ArgumentNullException("ILogger is null");
            _metrics = metrics ?? throw new ArgumentNullException("IMetrics is null");
            _queueSettings = queueSettings ?? throw new ArgumentNullException("MessageQueueSettings is null");
            _config = config ?? throw new ArgumentNullException("IConfiguration is null");

            _serviceProvider = sp;

            InitializeReconnectChecker(_logger);

            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        public void Start()
        {
            try
            {
                _client = new MQClient(_queueSettings);

                Subscribe();

                InitializeUnsentQueueCleaner(_logger);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Issue occured during subscriber start");
            }
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
                        if (_memoryCache.TryGetValue(msg.MessageId, out string key))
                        {
                            try
                            {
                                _logger.LogInformation($"Message already handled before: {msg.MessageId}");
                                _client.Ack(msg.DeliveryTag);
                            }
                            catch (Exception) { }
                            return;
                        }

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
                            // If Ack fails, Message will be requeued again. So cache the message id on exception so we do not process it again.
                            try
                            {
                                _logger.LogInformation($"Message acknowledged and processed. MessageId: {msg.MessageId}");
                                _client.Ack(msg.DeliveryTag);
                            }
                            catch(Exception ex)
                            {
                                _logger.LogError(ex, $"Couldn't acknowledge MQ message: {msg.MessageId}. Message Id has been cached so we never reprocess it if it is requeued.");

                                if (!_memoryCache.TryGetValue(msg.MessageId, out string a))
                                    _memoryCache.Set(msg.MessageId, msg.MessageId, DateTimeOffset.UtcNow.AddHours(6));
                            }
                        }
                        else
                        {
                            try
                            {
                                _client.Ack(msg.DeliveryTag);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Couldn't ack failed message {msg.MessageId}.");
                                throw;
                            }

                            // Requeue up to msg expiry time or up until msg has been requeued 240+ times (2 hours....requeued every 30 sec, so 120 times an hour and 120 for 2 hours)
                            if (DateTime.UtcNow < payload.EmailPayload.ExpiryUtc && msg.Count < 240)
                            {
                                _logger.LogInformation($"Message Requeued due to failed processing. MessageId: {msg.MessageId}");
                                var task = Task.Run(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(30));
                                    
                                    msg.Count++;

                                    //_client.Requeue(msg.DeliveryTag);
                                    var published = _client.Publish(_queueSettings.Exchange, msg);
                                    if (!published)
                                        _logger.LogInformation($"Failed to requeue message (MessageId {msg.MessageId})");
                                });
                            }
                            else
                            {
                                _logger.LogInformation($"Message sent to 'Unsent' queue '{_queueSettings.UnsentQueue}' because of expiry or due to {msg.Count} failed send attempts. MessageId: {msg.MessageId}");

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
                    _logger.LogError(ex, $"Error occured while processing MQ message: {msg.MessageId}");
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

                    _metrics.Metric("Email", 0, new string[] { emailPayloadConfig.EmailPayload.EmailName, string.Join(",", response.Select(r => r.EmailAddress)) });

                    _logger.LogError($"Failed to email recipients: {string.Join(", ", response.Where(m => !m.EmailTriggered).Select(m => m.EmailAddress))}.");
                }
                else if (response.Any(m => m.EmailTriggered))
                {
                    _metrics.Metric("Email", 1, new string[] { emailPayloadConfig.EmailPayload.EmailName });
                }

                if (response.All(m => m.EmailSentSuccess))
                {
                    _metrics.ServiceCheck("Email", ServiceStatus.OK, new string[] { _config["Env"] });
                    _logger.LogInformation("Email sent successully!");
                }
            }
            catch (Exception ex)
            {
                _metrics.ServiceCheck("Email", ServiceStatus.CRITICAL, new string[] { _config["Env"] });
                _logger.LogError(ex, $"Email attempt not successful. Will be re-queued for retry. {Environment.NewLine} {_emailManager.SanitizeMessageForLogging(JsonConvert.SerializeObject(emailPayloadConfig))}");

                return false;

                //*********************************************************************************************
                // We were only requeuing Transient exceptions in this commented out block. Now we will requeue all types of exceptions.
                /*
                // If exception is Transient, message will be requeued for a retry. "IsTransientException" is inserted to Data in a Polly callback (see start.cs)
                if (ex.Data.Contains("IsTransientException"))
                {
                    return false;
                }

                _metrics.ServiceCheck("Email", ServiceStatus.CRITICAL, new string[] { _config["Env"] });
                _logger.LogError(ex, $"Email attempt not successful. Error not transient. {Environment.NewLine} {_emailManager.SanitizeMessageForLogging(JsonConvert.SerializeObject(emailPayloadConfig))}");
                */
            }
            return true;
        }

        private void InitializeUnsentQueueCleaner(ILogger<Subscriber> _logger)
        {
            // Empty out the Unsent queue for messages that are 4+ days old
            _unsentQueueTimer = new Timer();
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
        /// In case the auto reconnect fails, try to force re-connect.
        /// </summary>
        private void InitializeReconnectChecker(ILogger<Subscriber> _logger)
        {
            _reconnectTimer = new Timer();
            _reconnectTimer.Interval = TimeSpan.FromMinutes(10).TotalMilliseconds;
            _reconnectTimer.Elapsed += (object o, ElapsedEventArgs args) =>
            {
                try
                {
                    lock (_clientLock)
                    {
                        if (_client == null || !_client.IsAlive)
                        {
                            _logger.LogInformation("Reconnecting Consumer to MQ!");

                            if (_client != null)
                                _client.Dispose();
                            _unsentQueueTimer.Dispose();
                            Start();
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
            if (_unsentQueueTimer != null)
                _unsentQueueTimer.Dispose();

            if (_reconnectTimer != null)
                _reconnectTimer.Dispose();

            if (_client != null)
                _client.Dispose();
        }
    }
}
