using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class NavigationTests(ITestOutputHelper output)
{
    private const string HomeHeading = "Gather everyone, without the group-chat chaos.";

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

        // The counter is the interactive harness for verifying that a renderer
        // attaches and that in-app navigation keeps the same document.
        await page.GotoAsync("/counter");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Counter", Exact = true }).WaitForAsync();
        await page.WaitForFunctionAsync("() => typeof Blazor !== 'undefined'");
        // The button is disabled during prerendering until the renderer is interactive.
        await page.Locator("button:not([disabled])").Filter(new() { HasText = "Click me" }).WaitForAsync();
        var increment = page.GetByRole(AriaRole.Button, new() { Name = "Click me", Exact = true });
        await increment.ClickAsync();
        await page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Current count: 1" }).WaitForAsync();
        (await page.GetByRole(AriaRole.Status).InnerTextAsync()).ShouldBe("Current count: 1");
        var origin = await page.EvaluateAsync<double>("performance.timeOrigin");

        await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(origin);
        (await page.Locator("[data-theme-toggle]").IsVisibleAsync()).ShouldBeTrue();
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth")).ShouldBeTrue();

        // The navigation must wrap instead of scrolling sideways: a single scrolling
        // row hides destinations behind an affordance nobody notices on a phone.
        var navigationOverflow = await page.EvaluateAsync<string?>("""
            () => {
                const rail = document.querySelector('.site-navigation');
                if (!rail) return 'the navigation rail is missing';
                if (rail.scrollWidth > rail.clientWidth + 1) {
                    return `the rail scrolls ${rail.scrollWidth}px inside ${rail.clientWidth}px`;
                }
                const clipped = [...rail.querySelectorAll('a, button')].filter(element => {
                    const box = element.getBoundingClientRect();
                    return box.left < -1 || box.right > innerWidth + 1;
                });
                return clipped.length === 0 ? null : `${clipped.length} navigation item(s) sit outside the viewport`;
            }
            """);
        navigationOverflow.ShouldBeNull();

        // Enlarged browser text must not make the grid overflow horizontally.
        await page.EvaluateAsync("() => { document.documentElement.style.fontSize = '20px'; }");
        (await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"))
            .ShouldBeTrue($"the layout must not overflow at {page.ViewportSize?.Width}px with enlarged text");
        errors.ShouldBeEmpty();
    }
}
