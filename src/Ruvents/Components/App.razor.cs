namespace Ruvents.Components;

public sealed partial class App(IHostEnvironment environment, IConfiguration configuration)
{
    private string ServiceWorkerScript => configuration.GetValue("Pwa:EnableOfflineFallback", !environment.IsDevelopment())
        ? "service-worker.js"
        : "service-worker.development.js";
}
