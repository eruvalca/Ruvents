using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class AccessibilityTests(ITestOutputHelper output)
{
    private const string HomeHeading = "Gather everyone, without the group-chat chaos.";

    [Fact]
    [Trait("Category", "BrowserSmoke")]
    public async Task CorePagesHaveOrderedHeadingsReadableControlsAndContrastAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents-web", timeout.Token);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = app.GetEndpoint("ruvents-web", "https").ToString(),
            IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = 1280, Height = 960 },
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var page = await context.NewPageAsync();
        try
        {
            await page.GotoAsync("/");
            await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();
            await page.WaitForFunctionAsync("() => typeof Blazor !== 'undefined'");
            await AssertAccessibilityAsync(page, "Home");

            await page.GotoAsync("/counter");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Counter", Exact = true }).WaitForAsync();
            await AssertAccessibilityAsync(page, "Counter");

            await page.GotoAsync("/Account/Login");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).WaitForAsync();
            await AssertAccessibilityAsync(page, "Login");

            await page.GotoAsync("/Account/Register");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Create your account", Exact = true }).WaitForAsync();
            await AssertAccessibilityAsync(page, "Register");

            // The dark palette must meet the same contrast budget as the light one.
            await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
            await page.GotoAsync("/");
            await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();
            await AssertAccessibilityAsync(page, "Home (dark)");

            await page.GotoAsync("/Account/Register");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Create your account", Exact = true }).WaitForAsync();
            await AssertAccessibilityAsync(page, "Register (dark)");
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"accessibility-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task AssertAccessibilityAsync(IPage page, string pageName)
    {
        // The navigation's section header renders a lower-level heading before the
        // page heading; scoring only decreases is valid, so assert the page's own
        // single level-1 heading and leave ordering to the axe rule below.
        (await page.Locator("h1").CountAsync()).ShouldBe(1, $"{pageName} must expose exactly one level-1 heading.");

        var result = await page.RunAxe(new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions
            {
                Type = "rule",
                Values = ["heading-order", "color-contrast"],
            },
        });
        var violations = result.Violations.Select(violation =>
            $"{violation.Id}: {string.Join(" | ", violation.Nodes.Select(node => $"{node.Target} ({node.Html})"))}").ToArray();
        violations.ShouldBeEmpty(pageName);

        var controls = await page.EvaluateAsync<double[]>("""
            () => {
                const sizes = [];
                const visit = root => {
                    for (const control of root.querySelectorAll('input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="submit"]):not([type="button"]), select, textarea')) {
                        if (control.getClientRects().length > 0) {
                            sizes.push(parseFloat(getComputedStyle(control).fontSize));
                        }
                    }
                    for (const element of root.querySelectorAll('*')) {
                        if (element.shadowRoot) visit(element.shadowRoot);
                    }
                };
                visit(document);
                return sizes;
            }
            """);
        var smallest = await page.EvaluateAsync<string>("""
            () => {
                let worst = null;
                const visit = root => {
                    for (const control of root.querySelectorAll('input:not([type="hidden"]):not([type="checkbox"]):not([type="radio"]):not([type="submit"]):not([type="button"]), select, textarea')) {
                        if (control.getClientRects().length === 0) continue;
                        const size = parseFloat(getComputedStyle(control).fontSize);
                        if (worst === null || size < worst.size) {
                            const host = control.getRootNode().host;
                            worst = { size, description: `<${control.tagName.toLowerCase()}> inside ${host ? `<${host.tagName.toLowerCase()} class="${host.className}">` : 'the light DOM'}` };
                        }
                    }
                    for (const element of root.querySelectorAll('*')) {
                        if (element.shadowRoot) visit(element.shadowRoot);
                    }
                };
                visit(document);
                return worst ? `${worst.description} at ${worst.size}px` : 'none';
            }
            """);
        if (controls.Length != 0)
        {
            controls.Min().ShouldBeGreaterThanOrEqualTo(16, $"{pageName}: smallest form control is {smallest}");
        }
    }
}
