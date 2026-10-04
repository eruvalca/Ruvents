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
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
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
            var email = $"cancellation-{Guid.NewGuid():N}@example.test";
            const string Password = "Test-only-Account!123";
            await page.GotoAsync("/Account/Register");
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

            var documentOrigin = await page.EvaluateAsync<double>("performance.timeOrigin");
            await page.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Welcome to Ruvents", Exact = true }).WaitForAsync();
            await page.GetByRole(AriaRole.Link, new() { Name = "Auth Required", Exact = true }).ClickAsync();
            await page.GetByText($"Hello {email}!", new() { Exact = true }).WaitForAsync();
            (await page.TitleAsync()).ShouldBe("Auth");
            (await page.EvaluateAsync<double>("performance.timeOrigin")).ShouldBe(documentOrigin);
            errors.ShouldBeEmpty();
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"account-summary-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }
}
