using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Email.API.MessageQueue
{
    public static class Extensions
    {
        public static AmqpTimestamp DateTimeToUnixTimeStamp(DateTime utc)
        {
            var dt = utc == DateTime.MinValue ? DateTime.UtcNow : utc;
            return new AmqpTimestamp((long)(dt.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))).TotalSeconds);
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dtDateTime;
        }

        public static MQMessage ConvertToMessage(this BasicDeliverEventArgs et)
        {
            var message = CreateMessage(et.Body.ToArray(), et.BasicProperties, et.RoutingKey);

            //message.ConsumerTag = et.ConsumerTag;
            message.DeliveryTag = et.DeliveryTag;
            message.Exchange = et.Exchange;
            //message.Redelivered = et.Redelivered;

            return message;
        }

        public static MQMessage CreateMessage(byte[] body, IBasicProperties props, string routingKey)
        {
            var message = new MQMessage();

            message.Body = body;
            message.AppId = props.AppId;
            message.MessageId = props.MessageId;
            message.Persistent = props.Persistent;

            if (props.Headers != null && props.Headers.ContainsKey("Count"))
            {
                message.Count = (int)props.Headers["Count"];
            }

            if (props.IsTimestampPresent())
            {
                message.TimestampUtc = UnixTimeStampToDateTime(props.Timestamp.UnixTime);
            }

            message.RoutingKey = routingKey;

            return message;
        }
    }
}
