using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Ruvents.Testing;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class NavigationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1280)]
    [InlineData(390)]
    [InlineData(320)]
    [Trait("Category", "BrowserSmoke")]
    public async Task NavigationPreservesDocumentAndCounterWorks(int width)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents-web", timeout.Token);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        // The isolated local AppHost uses the development HTTPS certificate.
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ruvents-web", "https").ToString(),
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        try
        {
            await VerifyNavigationAsync(page);
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"navigation-{width}-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task VerifyNavigationAsync(IPage page)
    {
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
        await page.WaitForFunctionAsync("() => typeof Blazor !== 'undefined'");
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");

        await page.GetByRole(AriaRole.Link, new() { Name = "Counter", Exact = true }).ClickAsync();
        // The button is disabled during prerendering until the renderer is interactive.
        await page.Locator("button:not([disabled])").Filter(new() { HasText = "Click me" }).WaitForAsync();
        var increment = page.GetByRole(AriaRole.Button, new() { Name = "Click me", Exact = true });
        await increment.ClickAsync();
        await page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Current count: 1" }).WaitForAsync();
        (await page.GetByRole(AriaRole.Status).InnerTextAsync()).ShouldBe("Current count: 1");
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);

        await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();

        // Enlarged browser text must not make the grid overflow horizontally.
        await page.EvaluateAsync("() => { document.documentElement.style.fontSize = '20px'; }");
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"))
            .ShouldBeTrue($"the layout must not overflow at {page.ViewportSize?.Width}px with enlarged text");
        errors.ShouldBeEmpty();
    }
}
