using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace Email.API.MessageQueue
{
    public class MQClient
    {
        IConnection _connection;
        IModel _channel;
        ConnectionFactory _factory;

        ConcurrentDictionary<ulong, bool> _publishStatus;

        public MQClient(MessageQueueSettings queueSettings)
        {
            if (!queueSettings.EnableMessageQueue)
                return;

            if (string.IsNullOrWhiteSpace(queueSettings.Uri))
                throw new ArgumentNullException("Eventbus uri missing in config");

            if (string.IsNullOrWhiteSpace(queueSettings.Exchange))
                throw new ArgumentNullException("Eventbus exchange missing in config");

            if (string.IsNullOrWhiteSpace(queueSettings.MainQueue) || string.IsNullOrWhiteSpace(queueSettings.UnsentQueue))
                throw new ArgumentNullException("Main queue or Unsent queue is missing in config");

            _factory = new ConnectionFactory() { Uri = new Uri(queueSettings.Uri) };
            _factory.AutomaticRecoveryEnabled = true;
            _factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10); // attempt recovery every 10 seconds if connection unexpectedly shutdowsn
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(queueSettings.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);

            // Main queue bindings
            _channel.QueueDeclare(queueSettings.MainQueue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queueSettings.MainQueue, queueSettings.Exchange, $"{queueSettings.Exchange}.{queueSettings.MainQueue}.*", null); 

            // Unsent queue bindings
            _channel.QueueDeclare(queueSettings.UnsentQueue, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(queueSettings.UnsentQueue, queueSettings.Exchange, $"{queueSettings.Exchange}.{queueSettings.UnsentQueue}.*", null);

            // Handler for broker acknowledged event
            _channel.BasicAcks += OnAcknowledgement;

            // Handler for broker unacknowledged event
            _channel.BasicNacks += OnUnAcknowledgement;

            _channel.ConfirmSelect(); // Enable publisher acknowledgements

            //_channel.BasicQos(0, 1, false);
            _connection.ConnectionShutdown += OnConnectionShutdown;

            _publishStatus = new ConcurrentDictionary<ulong, bool>();
        }

        public bool Publish(string exchange, MQMessage message)
        {
            _publishStatus.Clear();

            PublishInternal(exchange, message);
            _channel.WaitForConfirmsOrDie();

            return _publishStatus.All(m => m.Value == true);
        }

        public bool Publish(string exchange, List<MQMessage> messages)
        {
            _publishStatus.Clear();

            foreach(var message in messages)
            {
                PublishInternal(exchange, message);
            }
            _channel.WaitForConfirmsOrDie();

            return _publishStatus.All(m => m.Value == true);
        }

        private void PublishInternal(string exchange, MQMessage message)
        {
            if (!_publishStatus.ContainsKey(_channel.NextPublishSeqNo))
            {
                _publishStatus.TryAdd(_channel.NextPublishSeqNo, false);
            }

            var properties = _channel.CreateBasicProperties();
            properties.AppId = message.AppId;
            properties.MessageId = message.MessageId;
            properties.Persistent = message.Persistent;
            properties.Headers = new Dictionary<string, object>();
            properties.Headers.Add("Count", message.Count);
            properties.Timestamp = Extensions.DateTimeToUnixTimeStamp(message.TimestampUtc);

            _channel.BasicPublish(exchange, message.RoutingKey, mandatory: true, basicProperties: properties, message.Body);
        }

        public void Ack(ulong deliveryTag, bool multiple = false)
        {
            _channel.BasicAck(deliveryTag, multiple);
        }

        public void Requeue(ulong deliveryTag, bool multiple = false)
        {
            _channel.BasicNack(deliveryTag, multiple, true);
        }

        public void Drop(ulong deliveryTag)
        {
            _channel.BasicAck(deliveryTag, false);
        }

        public void DeadLetter(ulong deliveryTag)
        {
            _channel.BasicNack(deliveryTag, false, false);
        }

        public virtual void Subscribe(string queueName, string consumerName, Action<MQMessage> handler)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, et) =>
            {
                var message = et.ConvertToMessage();
                handler(message);
            };

            _channel.BasicConsume(queue: queueName,
                           autoAck: false,
                           consumerTag: consumerName,
                           consumer: consumer);
        }

        public MQMessage GetMessage(string queueName)
        {
            var result = _channel.BasicGet(queueName, false);

            if (result == null || result.Body.IsEmpty)
                return null;

            var message = Extensions.CreateMessage(result.Body.ToArray(), result.BasicProperties, result.RoutingKey);
            message.DeliveryTag = result.DeliveryTag;
            message.Count = (int)result.MessageCount;

            return message;
        }

        private void OnAcknowledgement(object sender, BasicAckEventArgs args)
        {
            if (_publishStatus.ContainsKey(args.DeliveryTag))
            {
                _publishStatus[args.DeliveryTag] = true;
            }
        }

        private void OnUnAcknowledgement(object sender, BasicNackEventArgs e)
        {
            // Handle UnAcks
        }

        private void OnConnectionShutdown(object sender, ShutdownEventArgs e) 
        { 

        }

        public bool IsAlive
        {
            get
            {
                return _connection != null && _connection.IsOpen;
            }
        }

        public void Dispose()
        {
            if (_channel != null && _channel.IsOpen)
                _channel.Close();

            if (_connection != null && _connection.IsOpen)
                _connection.Close();
        }
    }
}
