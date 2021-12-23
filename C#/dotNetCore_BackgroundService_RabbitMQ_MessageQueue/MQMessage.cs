using System;
using System.Text;

namespace Email.API.MessageQueue
{
    [Serializable]
    public class MQMessage
    {
        public byte[] Body { get; set; }
        public string AppId { get; set; }
        public string MessageId { get; set; }
        public bool Persistent { get; set; }
        public ulong DeliveryTag { get; set; }
        public string Exchange { get; set; }
        public DateTime TimestampUtc { get; set; }
        public int Count { get; set; }
        public string RoutingKey { get; set; }

        public override string ToString()
        {
            var s = $"MessageId:{MessageId}, DeliveryTag:{DeliveryTag}, Body:{Encoding.UTF8.GetString(Body)}";
            return s;
        }
    }
}
