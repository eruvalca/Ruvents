using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.PlaywrightTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
[Collection<BrowserAppHostDefinition>]
public sealed class RegistrationPhotoTests(ITestOutputHelper output)
{
    private const string HomeHeading = "Gather everyone, without the group-chat chaos.";
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    [Trait("Category", "BrowserSmoke")]
    public async Task PreparingACropDoesNotSubmitRegistrationAndThePhotoPageSavesExplicitlyAsync()
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
            var imagePath = FindRepositoryFile(Path.Combine("src", "Ruvents", "wwwroot", "icons", "icon-192.png"));

            await page.GotoAsync("/Account/Register");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Create your account", Exact = true }).WaitForAsync();
            await PrepareCropAsync(page, imagePath);

            // Capturing a crop must leave the page on the registration form, with the
            // crop in its hidden field and only the page-owned submit action.
            page.Url.ShouldContain("/Account/Register");
            (await page.Locator("input[name='PhotoData']").InputValueAsync()).ShouldNotBeNullOrEmpty();
            (await page.Locator("button[type='submit']").CountAsync()).ShouldBe(1);
            (await page.GetByRole(AriaRole.Button, new() { Name = "Save photo", Exact = true }).CountAsync()).ShouldBe(0);

            var email = $"photo-{Guid.NewGuid():N}@example.test";
            await page.Locator("input[name='Input.FirstName']").FillAsync("Ada");
            await page.Locator("input[name='Input.MiddleName']").FillAsync("Grace");
            await page.Locator("input[name='Input.LastName']").FillAsync("Lovelace");
            await page.Locator("input[name='Input.Email']").FillAsync(email);
            await page.Locator("input[name='Input.Password']").FillAsync(Password);
            await page.Locator("input[name='Input.ConfirmPassword']").FillAsync(Password);
            await page.GetByRole(AriaRole.Button, new() { Name = "Create account", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = HomeHeading, Exact = true }).WaitForAsync();

            // The standalone photo page keeps the crop tool's save button once a crop
            // exists and saves only when that button is chosen.
            await page.GotoAsync("/Account/Manage/Photo");
            await page.GetByRole(AriaRole.Heading, new() { Name = "Profile photo", Exact = true }).WaitForAsync();
            await PrepareCropAsync(page, imagePath);
            var savePhoto = page.GetByRole(AriaRole.Button, new() { Name = "Save photo", Exact = true });
            await savePhoto.WaitForAsync();
            await savePhoto.ClickAsync();
            await page.GetByText("Your profile photo has been updated").WaitForAsync();
        }
        finally
        {
            var artifacts = Path.Combine(AppContext.BaseDirectory, "TestResults", $"registration-photo-{Guid.NewGuid():N}");
            await BrowserArtifacts.CaptureAsync(page, context, artifacts, output.WriteLine);
        }
    }

    private static async Task PrepareCropAsync(IPage page, string imagePath)
    {
        await page.WaitForFunctionAsync(
            "() => { const input = document.querySelector('#crop-photo'); return input && !input.disabled; }");
        await page.Locator("#crop-photo").SetInputFilesAsync(imagePath);
        // Cropper.js has created its container once the image is ready to position.
        await page.Locator(".photo-crop .cropper-container").WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Use this crop", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync(
            "() => { const input = document.querySelector(\"input[name='PhotoData']\"); return input && input.value.length > 0; }");
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ruvents.slnx")))
            {
                var path = Path.Combine(directory.FullName, relativePath);
                File.Exists(path).ShouldBeTrue($"The browser test asset '{path}' must exist.");
                return path;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
    }
}
