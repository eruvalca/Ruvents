using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class PwaTests(ITestOutputHelper output)
{
    [Fact]
    public Task ManifestAndOfflineFallbackPreserveNetworkBoundariesAsync()
        => WithPageAsync(enableOfflineFallback: true, VerifyOfflineFallbackAsync);

    [Fact]
    public Task DevelopmentDefaultsToNetworkOnlyAsync()
        => WithPageAsync(enableOfflineFallback: false, async (page, context) =>
        {
            await page.GotoAsync("/");
            await WaitForWorkerAsync(page, "service-worker.development.js");
            (await OwnedCachesAsync(page)).ShouldBeEmpty();
            await context.SetOfflineAsync(true);
            await Should.ThrowAsync<PlaywrightException>(() => page.GotoAsync("/Account/Login?development-offline"));
        });

    [Fact]
    public Task ProductionUpdatesWaitAndDevelopmentRemovesOnlyApplicationCachesAsync()
        => WithPageAsync(enableOfflineFallback: true, async (page, _) =>
        {
            await page.GotoAsync("/");
            await WaitForWorkerAsync(page, "service-worker.js");
            (await OwnedCachesAsync(page)).ShouldBe(["ruvents-offline-v1"]);
            var documentOrigin = await page.EvaluateAsync<double>("performance.timeOrigin");
            await page.EvaluateAsync("() => navigator.serviceWorker.register('/service-worker.js?update-probe=1', { scope: '/', updateViaCache: 'none' })");
            await using var waiting = await page.WaitForFunctionAsync("async () => (await navigator.serviceWorker.getRegistration()).waiting?.scriptURL.endsWith('?update-probe=1')");
            (await page.EvaluateAsync<bool>("() => navigator.serviceWorker.controller.scriptURL.endsWith('/service-worker.js')")).ShouldBeTrue();
            (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(documentOrigin);
            await page.EvaluateAsync("async () => { await caches.open('ruvents-offline-obsolete'); await caches.open('unrelated-cache'); }");
            await page.EvaluateAsync("() => navigator.serviceWorker.register('/service-worker.development.js', { scope: '/', updateViaCache: 'none' })");
            await WaitForWorkerAsync(page, "service-worker.development.js");
            (await OwnedCachesAsync(page)).ShouldBeEmpty();
            (await page.EvaluateAsync<string[]>("() => caches.keys()")).ShouldContain("unrelated-cache", StringComparer.Ordinal);
        });

    private async Task WithPageAsync(bool enableOfflineFallback, Func<IPage, IBrowserContext, Task> verify)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(enableOfflineFallback, timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents", timeout.Token);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ruvents", "https").ToString(),
            IgnoreHTTPSErrors = true,
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        try
        {
            await verify(page, context);
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"pwa-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task VerifyOfflineFallbackAsync(IPage page, IBrowserContext context)
    {
        // A static resource establishes the origin without registering the worker yet.
        await page.GotoAsync("/offline.html");
        await page.EvaluateAsync("async () => { await caches.open('ruvents-offline-obsolete'); await caches.open('unrelated-cache'); }");
        await page.GotoAsync("/");
        await WaitForWorkerAsync(page, "service-worker.js");
        await VerifyManifestAsync(page);
        (await OwnedCachesAsync(page)).ShouldBe(["ruvents-offline-v1"]);
        (await page.EvaluateAsync<string[]>("() => caches.keys()")).ShouldContain("unrelated-cache", StringComparer.Ordinal);
        (await page.EvaluateAsync<string[]>("async () => (await (await caches.open('ruvents-offline-v1')).keys()).map(request => new URL(request.url).pathname)"))
            .ShouldBe(["/offline.html"]);
        (await page.EvaluateAsync<int>("async () => (await fetch('/api/account/summary')).status")).ShouldBe(401);
        (await page.EvaluateAsync<int>("async () => (await fetch('/missing-pwa-page')).status")).ShouldBe(404);

        await using var ready = await page.WaitForFunctionAsync("() => customElements.get('fluent-button') && typeof Blazor !== 'undefined'");
        var documentOrigin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await page.GetByRole(AriaRole.Link, new() { Name = "Login", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(documentOrigin);
        await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();

        await context.SetOfflineAsync(true);
        await VerifyNetworkFailuresAsync(page);
        // This click starts as enhanced navigation, then falls back to a standalone document.
        await page.GetByRole(AriaRole.Link, new() { Name = "Login", Exact = true }).ClickAsync();
        await VerifyOfflineDocumentAsync(page);
        page.Url.ShouldEndWith("/Account/Login");
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldNotBe(documentOrigin);
        await context.SetOfflineAsync(false);
        await page.GetByRole(AriaRole.Link, new() { Name = "Try again", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).WaitForAsync();
        await context.SetOfflineAsync(true);
        await page.GotoAsync("/auth");
        await VerifyOfflineDocumentAsync(page);
        page.Url.ShouldEndWith("/auth");
    }

    private static async Task VerifyManifestAsync(IPage page)
    {
        (await page.Locator("link[rel='manifest']").GetAttributeAsync("href")).ShouldBe("manifest.webmanifest");
        var manifest = await page.EvaluateAsync<JsonElement>("async () => (await fetch('/manifest.webmanifest')).json()");
        manifest.GetProperty("name").GetString().ShouldBe("Ruvents");
        manifest.GetProperty("short_name").GetString().ShouldBe("Ruvents");
        manifest.GetProperty("id").GetString().ShouldBe("/");
        manifest.GetProperty("scope").GetString().ShouldBe("/");
        manifest.GetProperty("start_url").GetString().ShouldBe("/");
        manifest.GetProperty("display").GetString().ShouldBe("standalone");
        var icons = manifest.GetProperty("icons");
        icons.GetArrayLength().ShouldBe(2);
        foreach (var icon in icons.EnumerateArray())
        {
            var dimensions = await page.EvaluateAsync<int[]>("async src => { const image = new Image(); image.src = src; await image.decode(); return [image.naturalWidth, image.naturalHeight]; }", icon.GetProperty("src").GetString());
            $"{dimensions[0]}x{dimensions[1]}".ShouldBe(icon.GetProperty("sizes").GetString());
        }
    }

    private static async Task VerifyNetworkFailuresAsync(IPage page)
    {
        var failures = await page.EvaluateAsync<bool[]>("""
            async () => {
                const requests = [
                    ['/api/account/summary', {}],
                    ['/api/account/summary', { headers: { accept: 'text/html; blazor-enhanced-nav=on' } }],
                    ['/Account/Logout', { method: 'POST' }],
                    ['/missing-pwa-script.js', {}]
                ];
                return Promise.all(requests.map(async ([url, options]) => {
                    try { await fetch(url, options); return false; }
                    catch { return true; }
                }));
            }
            """);
        failures.ShouldBe([true, true, true, true]);
    }

    private static async Task VerifyOfflineDocumentAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Heading, new() { Name = "Connection required", Exact = true }).WaitForAsync();
        (await page.TitleAsync()).ShouldBe("Connection required - Ruvents");
        (await page.EvaluateAsync<bool>("typeof Blazor === 'undefined'")).ShouldBeTrue();
    }

    private static async Task WaitForWorkerAsync(IPage page, string file)
    {
        await using var condition = await page.WaitForFunctionAsync("file => navigator.serviceWorker.controller?.scriptURL.endsWith('/' + file) && navigator.serviceWorker.controller.state === 'activated'", file);
    }

    private static Task<string[]> OwnedCachesAsync(IPage page)
        => page.EvaluateAsync<string[]>("async () => (await caches.keys()).filter(key => key.startsWith('ruvents-offline-'))");
}
