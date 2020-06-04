using Ruvents.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Ruvents.Client.Services
{
    public class RuventService
    {
        private readonly HttpClient client;

        public RuventService(HttpClient client)
        {
            this.client = client;
        }

        public async Task<List<Ruvent>> GetRuventsByMonthAndYear(int month, int year)
        {
            return await client.GetFromJsonAsync<List<Ruvent>>($"Ruvent/{month}/{year}");
        }

        public async Task<Ruvent> GetRuvent(int id)
        {
            return await client.GetFromJsonAsync<Ruvent>($"Ruvent/{id}");
        }

        public async Task<Ruvent> CreateRuvent(Ruvent ruvent)
        {
            var response = await client.PostAsJsonAsync("Ruvent", ruvent);
            return await response.Content.ReadFromJsonAsync<Ruvent>();
        }

        public async Task<Ruvent> UpdateRuvent(int id, Ruvent ruvent)
        {
            var response = await client.PutAsJsonAsync($"Ruvent/{id}", ruvent);
            return await response.Content.ReadFromJsonAsync<Ruvent>();
        }

        public async Task DeleteRuvent(int id)
        {
            await client.DeleteAsync($"Ruvent/{id}");
        }
    }
}
