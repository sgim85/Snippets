using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PB.Monitoring;
using System;
using System.Text;
using System.Timers;

namespace Email.API.MessageQueue
{
    public class Publisher : IDisposable
    {
        ILogger<Publisher> _logger;
        readonly IMetrics _metrics;
        MQClient _client;
        MessageQueueSettings _queueSettings;
        IConfiguration _config;
        Timer _reconnectTimer;
        readonly object _clientLock = new object();

        public Publisher(ILogger<Publisher> logger, IMetrics metrics, MessageQueueSettings queueSettings, IConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException("ILogger is null");
            _metrics = metrics ?? throw new ArgumentNullException("IMetrics is null");
            _queueSettings = queueSettings ?? throw new ArgumentNullException("MessageQueueSettings is null");
            _config = config ?? throw new ArgumentNullException("IConfiguration is null");

            try
            {
                _client = new MQClient(_queueSettings);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Producer RMQ Client cannot be initialized");
            }

            InitializeReconnectChecker(_logger);

            // ************ TEST Publish *************
            //for (int i = 0; i < 10; i++)
            //{
            //    Publish(new EmailPayloadConfig
            //    {
            //        EmailPayload = new PB.Email.EmailPayload { }
            //    });
            //}
        }


        /// <summary>
        /// Publishes email payload to message queue
        /// </summary>
        /// <param name="emailPayloadConfig"></param>
        /// <param name="messageId"></param>
        public (bool Published, string PublishedMessageId) Publish(EmailPayloadConfig emailPayloadConfig, string messageId = null)
        {
            var json = JsonConvert.SerializeObject(emailPayloadConfig);
            try
            {
                var message = new MQMessage();

                message.AppId = "email.API";
                message.MessageId = messageId ?? Guid.NewGuid().ToString();
                message.RoutingKey = _queueSettings.GetRoutingKey(_queueSettings.MainQueue);
                message.Persistent = true;

                message.Body = Encoding.UTF8.GetBytes(json);

                lock (_clientLock)
                {
                    // If queue connection is down, handle request normally without requeueing. 
                    // InitializeReconnectChecker() function will attempt to recover connection.
                    if (_client == null || !_client.IsAlive)
                    {
                        _logger.LogInformation("PRODUCER is not alive. Send message directly without queueing.");

                        return (false, message.MessageId);
                    }

                    var isPublished = _client.Publish(_queueSettings.Exchange, message);

                    if (isPublished)
                    {
                        _logger.LogInformation($"Message published to Queue. MessageId {message.MessageId}");
                        _metrics.Metric("PublishedEmailMessage", 1, new string[] { _config["Env"] });
                    }
                    else
                    {
                        _logger.LogWarning($"Publish to Queue failed. MessageId {message.MessageId}. {Environment.NewLine} {json}");
                        _metrics.Metric("PublishedEmailMessage", 0, new string[] { _config["Env"] });
                        return (false, null);
                    }
                }

                return (true, message.MessageId);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Message could not be published. {Environment.NewLine} {json}");
                return (false, null);
            }
        }

        /// <summary>
        /// RabbitMQ automatically reconnects if there is an unexpected failure.
        /// In case the auto reconnect fails, try to force re-connect.
        /// </summary>
        private void InitializeReconnectChecker(ILogger<Publisher> _logger)
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
                            _logger.LogInformation("Reconnecting Producer to MQ!");

                            if (_client != null)
                                _client.Dispose();
                            _client = new MQClient(_queueSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PRODUCER RECONNECT failed due to exception.");
                }
            };
            _reconnectTimer.Enabled = true;
        }

        public void Dispose()
        {
            if (_reconnectTimer != null)
                _reconnectTimer.Dispose();

            if (_client != null)
                _client.Dispose();
        }
    }
}
