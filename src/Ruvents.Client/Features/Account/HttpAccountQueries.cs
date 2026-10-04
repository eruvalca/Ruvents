using System.Net;
using System.Net.Http.Json;
using Ruvents.SharedKernel.Features.Account;

namespace Ruvents.Client.Features.Account;

internal sealed class HttpAccountQueries(HttpClient http) : IAccountQueries
{
    public async Task<AccountSummary?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("api/account/summary", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountSummary>(cancellationToken);
    }
}
