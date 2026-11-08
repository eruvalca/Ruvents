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

    // The static registration form accepts a plain file upload when JavaScript is off.
    private const string OnePixelPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private const string PlanningClientHeader = "X-Ruvents-Client";
    private const string PlanningClientValue = "planning-ui";

    private static readonly Dictionary<string, string> _clientHeaders = new(StringComparer.Ordinal)
    {
        [PlanningClientHeader] = PlanningClientValue,
    };

    [Fact]
    [Trait("Category", "BrowserSmoke")]
    public async Task EventTimesFollowTheBrowserZoneThroughServerAndWebAssemblyAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents-web", timeout.Token);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var baseUrl = app.GetEndpoint("ruvents-web", "https").ToString();

        var registrationState = await RegisterWithoutJavaScriptAsync(browser, baseUrl);
        var (groupId, storageState) = await SeedFamilyAndEventAsync(browser, baseUrl, registrationState);

        // A fresh context has no WebAssembly cache, so the first navigation uses Blazor
        // Server; the storage state already carries the cookie the page script wrote.
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = baseUrl,
            IgnoreHTTPSErrors = true,
            TimezoneId = "Asia/Tokyo",
            StorageState = storageState,
        });

        // The raw response proves the server-side render used the cookie's zone.
        var html = await (await context.APIRequest.GetAsync($"/groups/{groupId}")).TextAsync();
        html.ShouldContain("3:00 AM");
        html.ShouldContain("Event time");
        html.ShouldContain("(UTC)");

        var page = await context.NewPageAsync();
        var circuitOpened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtimeDownloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
        page.Response += (_, response) =>
        {
            if (response.Url.Contains("dotnet.native.", StringComparison.Ordinal))
            {
                runtimeDownloaded.TrySetResult();
            }
        };

        await page.GotoAsync($"/groups/{groupId}");
        await circuitOpened.Task.WaitAsync(TimeSpan.FromSeconds(30), timeout.Token);
        await AssertTokyoTimeAsync(page);
        (await page.EvaluateAsync<string>("() => document.cookie")).ShouldContain("ruvents-tz=Asia%2FTokyo");

        // The Server render downloads and initializes the WebAssembly runtime in the
        // background; the next navigation then uses the browser renderer.
        await runtimeDownloaded.Task.WaitAsync(TimeSpan.FromSeconds(90), timeout.Token);
        await page.WaitForFunctionAsync(
            "() => navigator.serviceWorker && navigator.serviceWorker.controller !== null",
            null,
            new() { Timeout = 30_000 });

        var wasmStarted = false;
        for (var attempt = 0; attempt < 3 && !wasmStarted; attempt++)
        {
            socketsAfterReload.Clear();
            trackReloadSockets = true;
            await page.ReloadAsync();
            try
            {
                await page.WaitForFunctionAsync(
                    "() => window.Blazor && Blazor.runtime && Object.keys(Blazor.runtime).length > 0",
                    null,
                    new() { Timeout = 20_000 });
                wasmStarted = true;
            }
            catch (TimeoutException)
            {
                await page.WaitForTimeoutAsync(3_000);
            }
        }

        wasmStarted.ShouldBeTrue($"circuits after reload: {string.Join(", ", socketsAfterReload)}");
        socketsAfterReload.ShouldBeEmpty();
        await AssertTokyoTimeAsync(page);
        output.WriteLine("A UTC event showed 3:00 AM in Tokyo under both the Server and WebAssembly renderers.");
    }

    private static async Task AssertTokyoTimeAsync(IPage page)
    {
        var card = page.Locator("li").Filter(new()
        {
            Has = page.GetByRole(AriaRole.Link, new() { Name = EventTitle, Exact = true }),
        });
        await card.WaitForAsync();
        (await card.Locator(".event-when").InnerTextAsync()).ShouldContain("3:00 AM");
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
