using System;
using System.Collections.Generic;
using System.Text;

namespace Ruvents.Shared
{
    public class NotificationSubscription
    {
        public int NotificationSubscriptionId { get; set; }

        public string Sub { get; set; }

        public string Url { get; set; }

        public string P256dh { get; set; }

        public string Auth { get; set; }
    }
}
