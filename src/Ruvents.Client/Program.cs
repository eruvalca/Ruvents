using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Ruvents.Client.Planning;
using Ruvents.UI.Planning;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddSingleton(TimeProvider.System);
// The WebAssembly renderer knows the browser zone from TimeZoneInfo.Local directly.
builder.Services.AddScoped<IViewerTimeZone, LocalTimeZone>();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddHttpClient<IPlanningApi, HttpPlanningApi>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    client.DefaultRequestHeaders.Add("X-Ruvents-Client", "planning-ui");
});

await builder.Build().RunAsync();
