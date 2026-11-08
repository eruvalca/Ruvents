using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class TimeZoneHandoffTests(ITestOutputHelper output)
{
    private const string HomeHeading = "Gather everyone, without the group-chat chaos.";
    private const string Email = "browser-zone@example.test";
    private const string Password = "Browser1!pass";
    private const string GroupName = "Browser Timezone Family";
    private const string EventTitle = "Global Call";
    private const string PlanningClientHeader = "X-Ruvents-Client";
    private const string PlanningClientValue = "planning-ui";

    // The static registration form accepts a plain file upload when JavaScript is off.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly Dictionary<string, string> _clientHeaders = new(StringComparer.Ordinal)
    {
        [PlanningClientHeader] = PlanningClientValue,
    };

    [Fact]
    [Trait("Category", "BrowserSmoke")]
    public async Task TimeZoneHandoffWorksWithAndWithoutAPrerenderedCookieAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(6));

        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents-web", timeout.Token);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var baseUrl = app.GetEndpoint("ruvents-web", "https").ToString();

        var registrationState = await RegisterWithoutJavaScriptAsync(browser, baseUrl);
        var (groupId, cookieState) = await SeedFamilyAndEventAsync(browser, baseUrl, registrationState);

        // With the cookie, the prerender already uses the viewer's zone.
        await VerifyHandoffAsync(browser, baseUrl, groupId, cookieState, expectsFallbackInMarkup: false, timeout.Token);

        // Without it, the prerender falls back to the event's zone and the Server
        // renderer corrects the card once it can ask the browser.
        await VerifyHandoffAsync(browser, baseUrl, groupId, registrationState, expectsFallbackInMarkup: true, timeout.Token);

        output.WriteLine("The UTC event rendered as 3:00 AM in Tokyo with and without a prerendered cookie, and after the WebAssembly handoff.");
    }

    private static async Task VerifyHandoffAsync(
        IBrowser browser,
        string baseUrl,
        Guid groupId,
        string storageState,
        bool expectsFallbackInMarkup,
        CancellationToken cancellationToken)
    {
        // A fresh context has no WebAssembly cache, so the first navigation uses Blazor Server.
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = baseUrl,
            IgnoreHTTPSErrors = true,
            TimezoneId = "Asia/Tokyo",
            StorageState = storageState,
        });

        // The raw response shows which zone the server-side render used.
        var markup = await (await context.APIRequest.GetAsync($"/groups/{groupId}")).TextAsync();
        if (expectsFallbackInMarkup)
        {
            markup.ShouldContain("6:00 PM");
            markup.ShouldNotContain("3:00 AM");
        }
        else
        {
            markup.ShouldContain("3:00 AM");
            markup.ShouldContain("Event time");
            markup.ShouldContain("(UTC)");
        }

        var page = await context.NewPageAsync();
        var circuitOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var socketsAfterReload = new ConcurrentQueue<string>();
        var trackReloadSockets = false;
        page.WebSocket += (_, socket) =>
        {
            if (!socket.Url.Contains("/_blazor", StringComparison.Ordinal))
            {
                return;
            }

            if (trackReloadSockets)
            {
                socketsAfterReload.Enqueue(socket.Url);
            }
            else
            {
                circuitOpened.TrySetResult();
            }
        };

        await page.GotoAsync($"/groups/{groupId}");
        await circuitOpened.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        await AssertTokyoTimeAsync(page);
        (await page.EvaluateAsync<string>("() => document.cookie")).ShouldContain("ruvents-tz=Asia%2FTokyo");

        // The Server render prefetches the whole WebAssembly runtime. Waiting for the
        // network to settle is the readiness signal the next navigation needs; it avoids
        // guessing with a delay and makes the reload deterministic.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 120_000 });

        trackReloadSockets = true;
        await page.ReloadAsync();
        await page.WaitForFunctionAsync(
            "() => window.Blazor && Blazor.runtime && Object.keys(Blazor.runtime).length > 0",
            null,
            new() { Timeout = 60_000 });

        socketsAfterReload.ShouldBeEmpty();
        await AssertTokyoTimeAsync(page);
    }

    private static async Task AssertTokyoTimeAsync(IPage page)
    {
        var card = page.Locator("li").Filter(new()
        {
            Has = page.GetByRole(AriaRole.Link, new() { Name = EventTitle, Exact = true }),
        });
        var when = card.Locator(".event-when");
        // The fallback case starts at the event's zone and changes here once the
        // interactive renderer confirms the browser zone.
        await when.Filter(new() { HasText = "3:00 AM" }).WaitForAsync(new() { Timeout = 30_000 });
        (await when.InnerTextAsync()).ShouldContain("3:00 AM");

        var eventZone = await card.Locator(".event-zone").InnerTextAsync();
        eventZone.ShouldContain("6:00 PM");
        eventZone.ShouldContain("(UTC)");
    }

    private static async Task<string> RegisterWithoutJavaScriptAsync(IBrowser browser, string baseUrl)
    {
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = baseUrl,
            IgnoreHTTPSErrors = true,
            JavaScriptEnabled = false,
            TimezoneId = "Asia/Tokyo",
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/Account/Register");
        await page.GetByLabel("Email", new() { Exact = true }).FillAsync(Email);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
        await page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(Password);
        await page.Locator("input[name='Photo']").SetInputFilesAsync(new FilePayload
        {
            Name = "avatar.png",
            MimeType = "image/png",
            Buffer = Convert.FromBase64String(OnePixelPng),
        });
        await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();
        return await context.StorageStateAsync();
    }

    private static async Task<(Guid GroupId, string StorageState)> SeedFamilyAndEventAsync(
        IBrowser browser,
        string baseUrl,
        string storageState)
    {
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = baseUrl,
            IgnoreHTTPSErrors = true,
            TimezoneId = "Asia/Tokyo",
            StorageState = storageState,
        });

        var groupResponse = await context.APIRequest.PostAsync(
            "/api/planning/families",
            new()
            {
                Headers = _clientHeaders,
                DataObject = new { name = GroupName, description = (string?)null },
            });
        groupResponse.Ok.ShouldBeTrue(await groupResponse.TextAsync());
        var groupId = JsonSerializer.Deserialize<Guid>(await groupResponse.TextAsync());

        var start = new DateTimeOffset(2030, 1, 15, 18, 0, 0, TimeSpan.Zero);
        var eventResponse = await context.APIRequest.PostAsync(
            "/api/planning/events",
            new()
            {
                Headers = _clientHeaders,
                DataObject = new
                {
                    familyGroupId = groupId,
                    title = EventTitle,
                    description = (string?)null,
                    locationName = "Anywhere",
                    locationAddress = (string?)null,
                    mapLink = (string?)null,
                    startUtc = start,
                    endUtc = start.AddHours(1),
                    isAllDay = false,
                    timeZoneId = "UTC",
                    rsvpDeadlineUtc = (DateTimeOffset?)null,
                },
            });
        eventResponse.Ok.ShouldBeTrue(await eventResponse.TextAsync());

        // Let the page script record the browser zone, then carry that cookie forward.
        var page = await context.NewPageAsync();
        await page.GotoAsync("/");
        await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();
        (await page.EvaluateAsync<string>("() => document.cookie")).ShouldContain("ruvents-tz=Asia%2FTokyo");

        return (groupId, await context.StorageStateAsync());
    }
}
