using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class NavigationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    public async Task FluentNavigationPreservesDocumentAndCounterWorks(int width)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents", timeout.Token);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        // The isolated local AppHost uses the development HTTPS certificate.
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ruvents", "https").ToString(),
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        try
        {
            await VerifyNavigationAsync(page, width);
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"navigation-{width}-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task VerifyNavigationAsync(IPage page, int width)
    {
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
        await page.WaitForFunctionAsync("() => customElements.get('fluent-button') && typeof Blazor !== 'undefined'");
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");

        await OpenNavigationAsync(page, width);
        var counter = page.GetByRole(AriaRole.Link, new() { Name = "Counter", Exact = true });
        await counter.Locator("svg").First.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        (await counter.Locator("svg").CountAsync()).ShouldBeGreaterThan(0);
        await counter.ClickAsync();
        // A Fluent button's shadow button can exist before Blazor hydrates its host.
        await page.Locator("fluent-button:not([disabled])").Filter(new() { HasText = "Click me" }).WaitForAsync();
        var increment = page.GetByRole(AriaRole.Button, new() { Name = "Click me", Exact = true });
        await increment.ClickAsync();
        await page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Current count: 1" }).WaitForAsync();
        (await page.GetByRole(AriaRole.Status).InnerTextAsync()).ShouldBe("Current count: 1");
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await AssertDrawerClosedAsync(page, width);

        await OpenNavigationAsync(page, width);
        await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        await AssertDrawerClosedAsync(page, width);
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();
        errors.ShouldBeEmpty();
    }

    private static async Task OpenNavigationAsync(IPage page, int width)
    {
        if (width < 768)
        {
            await page.Locator("#site-menu").ClickAsync();
            await page.Locator("fluent-drawer[hamburger] dialog").WaitForAsync();
        }
    }

    private static async Task AssertDrawerClosedAsync(IPage page, int width)
    {
        if (width < 768)
        {
            // The custom-element host has no layout box; inspect its actual shadow dialog.
            var drawer = page.Locator("fluent-drawer[hamburger] dialog");
            await drawer.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            (await drawer.IsVisibleAsync()).ShouldBeFalse();
        }
    }
}
