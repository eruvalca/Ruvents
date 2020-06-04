using Ruvents.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Ruvents.Client.Services
{
    public class NotificationSubscriptionService
    {
        private readonly HttpClient client;

        public NotificationSubscriptionService(HttpClient client)
        {
            this.client = client;
        }

        public async Task SubscribeToNotifications(NotificationSubscription subscription)
        {
            var response = await client.PutAsJsonAsync("Notification", subscription);
            response.EnsureSuccessStatusCode();
        }
    }
}
