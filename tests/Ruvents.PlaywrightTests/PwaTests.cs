using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Ruvents.Testing;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class PwaTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    [Trait("Category", "BrowserSmoke")]
    public async Task InstallationAndOfflineFallbackPreserveOnlineOnlyDataAsync(int width)
    {
        await RunInBrowserAsync(width, VerifyInstallationAndOfflineAsync, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpdateWaitsForConsentAndPreservesOtherTabsAsync()
    {
        await RunInBrowserAsync(1280, VerifyUpdatesAsync, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AlreadyInstallingWorkerPromptsOnlyForAnExistingInstallationAsync(bool hasController)
    {
        await RunInBrowserAsync(1280, (page, _) => VerifyAlreadyInstallingUpdateAsync(page, hasController), TestContext.Current.CancellationToken);
    }

    private async Task RunInBrowserAsync(int width, Func<IPage, IBrowserContext, Task> verify, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Starting an AppHost, PostgreSQL, migrations and Chromium dominates these
        // cases; a busy machine can push startup past three minutes.
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents-web", timeout.Token);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ruvents-web", "https").ToString(),
            ViewportSize = new() { Width = width, Height = 900 },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        try
        {
            // Observe the application's own registration attempt. A second register()
            // call here could hide broken startup code. Keep the original rejection
            // so a missing browser trust prerequisite fails with its actual cause.
            await page.AddInitScriptAsync("""
                (() => {
                    const register = ServiceWorkerContainer.prototype.register;
                    ServiceWorkerContainer.prototype.register = async function (...args) {
                        try { return await register.apply(this, args); }
                        catch (error) {
                            window.ruventsWorkerRegistrationError = String(error);
                            throw error;
                        }
                    };
                })();
                """);
            await verify(page, context);
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"pwa-{width}-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task VerifyInstallationAndOfflineAsync(IPage page, IBrowserContext context)
    {
        await OpenControlledPageAsync(page);
        (await page.Locator("#pwa-update").IsVisibleAsync()).ShouldBeFalse();
        (await page.Locator("link[rel='apple-touch-icon']").GetAttributeAsync("sizes")).ShouldBe("180x180");
        var manifest = await page.EvaluateAsync<JsonElement>("async () => (await fetch(document.querySelector('link[rel=manifest]').href)).json()");
        manifest.GetProperty("name").GetString().ShouldBe("Ruvents");
        manifest.GetProperty("display").GetString().ShouldBe("standalone");
        manifest.GetProperty("start_url").GetString().ShouldBe("/");
        manifest.GetProperty("background_color").GetString().ShouldBe("#FFFFFF");
        manifest.GetProperty("orientation").GetString().ShouldBe("any");
        manifest.GetProperty("display_override").EnumerateArray().Select(item => item.GetString()).ShouldContain(item => string.Equals(item, "standalone", StringComparison.Ordinal));
        var shortcuts = manifest.GetProperty("shortcuts").EnumerateArray().ToArray();
        shortcuts.Select(item => item.GetProperty("url").GetString()).ShouldContain(item => string.Equals(item, "/", StringComparison.Ordinal));
        shortcuts.ShouldAllBe(item => item.GetProperty("name").GetString() != null);
        foreach (var icon in manifest.GetProperty("icons").EnumerateArray())
        {
            (await page.EvaluateAsync<bool>("async src => { const image = new Image(); image.src = src; await image.decode(); return image.naturalWidth >= 192; }", icon.GetProperty("src").GetString())).ShouldBeTrue();
        }

        await VerifyInstallActionAsync(page);
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await page.Locator(".site-name").ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);

        // Pages are served from the network; the worker must not cache them.
        await page.GotoAsync("/counter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Counter", Exact = true }).WaitForAsync();
        await AssertPublicCacheOnlyAsync(page);
        origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await context.SetOfflineAsync(true);
        foreach (var method in new[] { "GET", "POST" })
        {
            (await page.EvaluateAsync<bool>("async method => { try { await fetch('/counter', { method }); return true; } catch { return false; } }", method)).ShouldBeFalse();
        }

        // Exercise Blazor's enhanced GET, not just full document navigation.
        await page.Locator(".site-name").ClickAsync();
        await AssertOfflineScreenAsync(page);
        await page.ScreenshotAsync(new() { Path = Path.Combine(AppContext.BaseDirectory, "TestResults", $"pwa-offline-{page.ViewportSize?.Width}.png"), FullPage = true });
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await context.SetOfflineAsync(false);
        await WaitForNetworkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Try again", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();

        await page.GotoAsync("/counter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Counter", Exact = true }).WaitForAsync();
        var deepUrl = page.Url;
        await context.SetOfflineAsync(true);
        var response = await page.ReloadAsync();
        response.ShouldNotBeNull().Status.ShouldBe(503);
        page.Url.ShouldBe(deepUrl);
        await AssertOfflineScreenAsync(page);
        await AssertPublicCacheOnlyAsync(page);
        await context.SetOfflineAsync(false);
        await WaitForNetworkAsync(page);
        await page.GetByRole(AriaRole.Link, new() { Name = "Try again", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Counter", Exact = true }).WaitForAsync();
        page.Url.ShouldBe(deepUrl);
    }

    private static async Task WaitForNetworkAsync(IPage page)
    {
        // Chrome clears offline emulation asynchronously. Wait until a request the
        // service worker does not handle succeeds, otherwise the worker can still
        // serve the offline fallback document for the retry navigation.
        // WaitForFunctionAsync has no options-only overload: pass the predicate
        // argument explicitly so the timeout reaches the options parameter.
        await page.WaitForFunctionAsync(
            "async () => { try { await fetch('/counter'); return true; } catch { return false; } }",
            arg: null,
            options: new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    private static async Task VerifyInstallActionAsync(IPage page)
    {
        // OS installation UI is browser-owned. Exercise our prompt handling with
        // the same event contract, without claiming to automate that native UI.
        await page.EvaluateAsync("""
            () => {
                const prompt = new Event('beforeinstallprompt', { cancelable: true });
                prompt.prompt = async () => { document.documentElement.dataset.installRequested = 'true'; };
                prompt.userChoice = Promise.resolve({ outcome: 'accepted' });
                window.dispatchEvent(prompt);
            }
            """);
        await page.Locator("[data-pwa-install-help] summary").ClickAsync();
        await page.Locator("[data-pwa-install]").ClickAsync();
        (await page.Locator("html").GetAttributeAsync("data-install-requested")).ShouldBe("true");
        await page.EvaluateAsync("window.dispatchEvent(new Event('appinstalled'))");
        (await page.Locator("[data-pwa-install-message]").InnerTextAsync()).ShouldBe("Ruvents is installed.");
        (await page.Locator("[data-pwa-install-action]").IsVisibleAsync()).ShouldBeFalse();
    }

    private static async Task AssertOfflineScreenAsync(IPage page)
    {
        await page.GetByRole(AriaRole.Heading, new() { Name = "Ruvents can’t connect right now", Exact = true }).WaitForAsync();
        (await page.Locator("main img").EvaluateAsync<bool>("async image => { await image.decode(); return image.naturalWidth === 192; }")).ShouldBeTrue();
        (await page.GetByRole(AriaRole.Link, new() { Name = "Try again", Exact = true }).IsVisibleAsync()).ShouldBeTrue();
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
        (await page.Locator("main").InnerTextAsync()).ShouldContain("If a save was interrupted");
    }

    private static async Task AssertPublicCacheOnlyAsync(IPage page)
    {
        var paths = await page.EvaluateAsync<string[]>("""
            async () => {
                const paths = [];
                for (const name of await caches.keys()) {
                    if (!name.startsWith('ruvents-offline-')) continue;
                    for (const request of await (await caches.open(name)).keys()) paths.push(new URL(request.url).pathname);
                }
                return paths.sort();
            }
            """);
        paths.ShouldBe(["/icons/icon-192.png", "/offline.html", "/pwa/offline.css"]);
    }

    private static async Task VerifyUpdatesAsync(IPage page, IBrowserContext context)
    {
        await OpenControlledPageAsync(page);
        var other = await context.NewPageAsync();
        await OpenControlledPageAsync(other);
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync("first@example.test");
        await other.GetByLabel("Email", new() { Exact = true }).FillAsync("second@example.test");
        var firstOrigin = await page.EvaluateAsync<double>("performance.timeOrigin");
        var secondOrigin = await other.EvaluateAsync<double>("performance.timeOrigin");

        // A distinct script URL starts a real worker update without mutating shared
        // build outputs underneath concurrent browser tests or adding a test API.
        await page.EvaluateAsync("""
            async version => {
                await caches.open('ruvents-offline-obsolete');
                await caches.open('unrelated-test-cache');
                await navigator.serviceWorker.register('/service-worker.js?test=' + version, { scope: '/', updateViaCache: 'none' });
            }
            """, Guid.NewGuid().ToString("N"));
        await page.WaitForFunctionAsync("async () => Boolean((await navigator.serviceWorker.getRegistration()).waiting)");
        await page.Locator("#pwa-update").WaitForAsync();
        await other.Locator("#pwa-update").WaitForAsync();
        (await page.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe("first@example.test");
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(firstOrigin);

        await other.Locator("[data-pwa-later]").ClickAsync();
        (await other.Locator("#pwa-update").IsVisibleAsync()).ShouldBeFalse();
        page.Dialog += DismissDialog;
        await page.Locator("[data-pwa-reload]").ClickAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(firstOrigin);
        (await page.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe("first@example.test");
        page.Dialog -= DismissDialog;
        page.Dialog += AcceptDialog;
        await page.Locator("[data-pwa-reload]").ClickAsync();
        await page.WaitForFunctionAsync("previous => performance.timeOrigin !== previous", firstOrigin);
        await page.GetByLabel("Email", new() { Exact = true }).WaitForAsync();
        await other.Locator("#pwa-update").WaitForAsync();
        (await other.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(secondOrigin);
        (await other.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe("second@example.test");
        var caches = await page.EvaluateAsync<string[]>("caches.keys()");
        caches.ShouldNotContain("ruvents-offline-obsolete", StringComparer.Ordinal);
        caches.ShouldContain("unrelated-test-cache", StringComparer.Ordinal);
        await AssertPublicCacheOnlyAsync(page);
    }

    private static async Task VerifyAlreadyInstallingUpdateAsync(IPage page, bool hasController)
    {
        // Control the browser event ordering while loading the real app/module:
        // updatefound has fired before register resolves, but installation is pending.
        await page.AddInitScriptAsync($$"""
            const installing = new EventTarget();
            installing.state = 'installing';
            const registration = new EventTarget();
            registration.installing = installing;
            registration.waiting = null;
            registration.update = async () => registration;
            const serviceWorker = new EventTarget();
            serviceWorker.controller = {{(hasController ? "{}" : "null")}};
            serviceWorker.register = async () => {
                registration.dispatchEvent(new Event('updatefound'));
                window.pwaRegistrationReturned = true;
                return registration;
            };
            Object.defineProperty(navigator, 'serviceWorker', { value: serviceWorker });
            window.finishPwaInstallation = () => {
                registration.installing = null;
                registration.waiting = installing;
                installing.state = 'installed';
                installing.dispatchEvent(new Event('statechange'));
            };
            """);
        await page.GotoAsync("/Account/Login");
        await page.WaitForFunctionAsync("() => window.pwaRegistrationReturned === true");
        (await page.Locator("#pwa-update").IsVisibleAsync()).ShouldBeFalse();
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync("unsaved@example.test");
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");
        await page.EvaluateAsync("window.finishPwaInstallation()");
        (await page.Locator("#pwa-update").IsVisibleAsync()).ShouldBe(hasController);
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        (await page.GetByLabel("Email", new() { Exact = true }).InputValueAsync()).ShouldBe("unsaved@example.test");
    }

    private static async Task OpenControlledPageAsync(IPage page)
    {
        await page.GotoAsync("/Account/Login");
        await page.GetByLabel("Email", new() { Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<string>("async () => (await fetch('/service-worker.js')).headers.get('content-type')")).ShouldStartWith("text/javascript");
        await page.WaitForFunctionAsync("() => Boolean(window.ruventsWorkerRegistrationError) || (Boolean(navigator.serviceWorker.controller) && typeof Blazor !== 'undefined')");
        (await page.EvaluateAsync<string?>("() => window.ruventsWorkerRegistrationError ?? null"))
            .ShouldBeNull("The application's service-worker registration must succeed; check the browser's HTTPS certificate trust if it fails.");
    }

    private static async void DismissDialog(object? sender, IDialog dialog) => await dialog.DismissAsync();

    private static async void AcceptDialog(object? sender, IDialog dialog) => await dialog.AcceptAsync();
}
