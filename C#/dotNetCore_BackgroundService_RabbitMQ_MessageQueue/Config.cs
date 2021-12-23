using System;
using System.Collections.Generic;

namespace Email.API.MessageQueue
{
    public class MessageQueueSettings
    {
        public string Uri { get; set; }
        public string Exchange { get; set; }
        public string MainQueue { get; set; }
        public string UnsentQueue { get; set; }
        public string GetRoutingKey(string queue)
        {
            return $"{Exchange}.{queue}.*";
        }
        public bool EnableMessageQueue { get; set; }
    }
}
