using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class AccountSummaryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task StaticIdentityFormsAuthenticateInteractiveAccountAndApiAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(enableOfflineFallback: true, timeout.Token);
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
        var page = await context.NewPageAsync();
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        var errors = new ConcurrentQueue<string>();
        page.PageError += (_, error) => errors.Enqueue(error);
        try
        {
            var email = $"account+{Guid.NewGuid():N}@example.test";
            const string Password = "Test-only-Account!123";
            await page.GotoAsync("/Account/Register");
            await using var worker = await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller?.scriptURL.endsWith('/service-worker.js') && navigator.serviceWorker.controller.state === 'activated'");
            await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
            await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
            await page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Click here to confirm your account" }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Confirm email", Exact = true }).WaitForAsync();
            await page.GotoAsync("/Account/Login?ReturnUrl=%2Fauth");
            await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
            await page.GetByLabel("Password", new() { Exact = true }).FillAsync(Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Log in", Exact = true }).ClickAsync();
            await page.GetByText($"Hello {email}!", new() { Exact = true }).WaitForAsync();
            (await page.TitleAsync()).ShouldBe("Auth");
            await page.GetByRole(AriaRole.Button, new() { Name = "Refresh account", Exact = true }).ClickAsync();
            await page.GetByText($"Hello {email}!", new() { Exact = true }).WaitForAsync();
            (await page.GetByText("Email confirmed: Yes", new() { Exact = true }).IsVisibleAsync()).ShouldBeTrue();

            var response = await context.APIRequest.GetAsync("/api/account/summary");
            try
            {
                response.Status.ShouldBe(200);
                var summary = await response.JsonAsync();
                summary.ShouldNotBeNull();
                summary.Value.GetProperty("userName").GetString().ShouldBe(email);
                summary.Value.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
            }
            finally
            {
                await response.DisposeAsync();
            }

            await VerifyPhoneRemovalAsync(page);
            await VerifyPlusAddressPasskeyAsync(page, context, email);
            var documentOrigin = await page.EvaluateAsync<double>("performance.timeOrigin");
            await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Auth Required", Exact = true }).ClickAsync();
            await page.GetByText($"Hello {email}!", new() { Exact = true }).WaitForAsync();
            (await page.TitleAsync()).ShouldBe("Auth");
            (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(documentOrigin);
            await page.GetByRole(AriaRole.Button, new() { Name = "Logout", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Log in", Exact = true }).WaitForAsync();
            (await page.EvaluateAsync<int>("async () => (await fetch('/api/account/summary')).status")).ShouldBe(401);
            (await page.Locator("body").InnerTextAsync()).ShouldNotContain($"Hello {email}!");
            (await page.EvaluateAsync<string[]>("async () => (await Promise.all((await caches.keys()).map(async key => (await (await caches.open(key)).keys()).map(request => new URL(request.url).pathname)))).flat()"))
                .ShouldBe(["/offline.html"]);
            errors.ShouldBeEmpty();
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"account-summary-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task VerifyPhoneRemovalAsync(IPage page)
    {
        await page.GotoAsync("/Account/Manage");
        var phone = page.GetByLabel("Phone number", new() { Exact = true });
        await phone.FillAsync("202-555-0147");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.GetByText("Your profile has been updated", new() { Exact = true }).WaitForAsync();
        await page.ReloadAsync();
        (await phone.InputValueAsync()).ShouldBe("202-555-0147");

        await phone.FillAsync(string.Empty);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
        await page.GetByText("Your profile has been updated", new() { Exact = true }).WaitForAsync();
        await page.ReloadAsync();
        (await phone.InputValueAsync()).ShouldBeEmpty();
        (await page.Locator(".error-text").AllTextContentsAsync()).ShouldAllBe(text => string.IsNullOrWhiteSpace(text));
    }

    private static async Task VerifyPlusAddressPasskeyAsync(IPage page, IBrowserContext context, string email)
    {
        // Exercise the explicit submit button without a competing conditional-autofill ceremony.
        await context.AddInitScriptAsync("PublicKeyCredential.isConditionalMediationAvailable = async () => false;");
        var cdp = await context.NewCDPSessionAsync(page);
        try
        {
            await cdp.SendAsync("WebAuthn.enable");
            await cdp.SendAsync("WebAuthn.addVirtualAuthenticator", new(StringComparer.Ordinal)
            {
                ["options"] = new
                {
                    protocol = "ctap2",
                    transport = "internal",
                    hasResidentKey = true,
                    hasUserVerification = true,
                    isUserVerified = true,
                    automaticPresenceSimulation = true,
                },
            });
            await page.GotoAsync("/Account/Manage/Passkeys");
            await page.GetByRole(AriaRole.Button, new() { Name = "Add a new passkey", Exact = true }).ClickAsync();
            await page.GetByLabel("Passkey name", new() { Exact = true }).FillAsync("Browser regression passkey");
            await page.GetByRole(AriaRole.Button, new() { Name = "Continue", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Cell, new() { Name = "Browser regression passkey", Exact = true }).WaitForAsync();
            await page.Locator(".site-navigation").GetByRole(AriaRole.Button, new() { Name = "Logout", Exact = true }).ClickAsync();
            await page.GotoAsync("/Account/Login?ReturnUrl=%2Fauth");
            await page.GetByLabel("Email", new() { Exact = true }).FillAsync(email);
            var response = await page.RunAndWaitForResponseAsync(
                () => page.GetByRole(AriaRole.Button, new() { Name = "Log in with a passkey", Exact = true }).ClickAsync(),
                response => string.Equals(new Uri(response.Url).AbsolutePath, "/Account/PasskeyRequestOptions", StringComparison.Ordinal));
            response.Status.ShouldBe(200);
            response.Url.ShouldContain($"username={Uri.EscapeDataString(email)}", Case.Sensitive);
            var options = await response.JsonAsync();
            options.ShouldNotBeNull();
            options.Value.GetProperty("allowCredentials").GetArrayLength().ShouldBe(1);
            await page.GetByText($"Hello {email}!", new() { Exact = true }).WaitForAsync();
        }
        finally
        {
            await cdp.DetachAsync();
        }
    }
}
