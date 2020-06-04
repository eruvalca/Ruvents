using Ruvents.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Ruvents.Client.Services
{
    public class AttendeeService
    {
        private readonly HttpClient client;

        public AttendeeService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<Attendee> CreateAttendee(Attendee attendee)
        {
            var response = await client.PostAsJsonAsync("Attendee", attendee);
            return await response.Content.ReadFromJsonAsync<Attendee>();
        }

        public async Task<Attendee> UpdateAttendee(int id, Attendee attendee)
        {
            var response = await client.PutAsJsonAsync($"Attendee/{id}", attendee);
            return await response.Content.ReadFromJsonAsync<Attendee>();
        }

        public async Task DeleteAttendee(int id)
        {
            await client.DeleteAsync($"Attendee/{id}");
        }
    }
}
