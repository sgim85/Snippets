using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PB.Monitoring;
using System;
using System.Text;

namespace Email.API.MessageQueue
{
    public class Publisher
    {
        ILogger<Publisher> _logger;
        readonly IMetrics _metrics;
        MQClient _client;
        MessageQueueSettings _queueSettings;
        IConfiguration _config;

        public Publisher(ILogger<Publisher> logger, IMetrics metrics, MessageQueueSettings queueSettings, IConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException("ILogger is null");
            _metrics = metrics ?? throw new ArgumentNullException("IMetrics is null");
            _queueSettings = queueSettings ?? throw new ArgumentNullException("MessageQueueSettings is null");
            _config = config ?? throw new ArgumentNullException("IConfiguration is null");

            _client = new MQClient(_queueSettings);

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

                if (!_client.IsAlive)
                {
                    _logger.LogInformation("PRODUCER is not alive. Attempting to reconnect.");

                    _client.Dispose();
                    _client = new MQClient(_queueSettings);
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

                return (true, message.MessageId);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, $"Message could not be published. {Environment.NewLine} {json}");
                return (false, null);
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
