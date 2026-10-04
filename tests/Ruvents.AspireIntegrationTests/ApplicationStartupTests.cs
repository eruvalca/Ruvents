using System.Diagnostics.CodeAnalysis;
using System.Net;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Ruvents.Testing;
using Shouldly;
using Xunit;

namespace Ruvents.AspireIntegrationTests;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class ApplicationStartupTests
{
    [Fact]
    public async Task AppHostCompletesMigrationsAndServesHealthyApplication()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await using var builder = await TestAppHost.CreateAsync(timeout.Token);
        builder.Resources.OfType<ContainerResource>().SelectMany(resource => resource.Annotations.OfType<ContainerMountAnnotation>())
            .ShouldBeEmpty();
        await using var app = await builder.BuildAsync(timeout.Token);
        await app.StartAsync(timeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("ruvents", timeout.Token);
        await app.ResourceNotifications.WaitForResourceAsync("ruvents-migrations", KnownResourceStates.Finished, timeout.Token);

        using var client = app.CreateHttpClient("ruvents", "https");
        foreach (var path in new[] { "/health", "/alive", "/", "/Account/Login" })
        {
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), timeout.Token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        }

        var home = await client.GetStringAsync(new Uri("/", UriKind.Relative), timeout.Token);
        home.ShouldContain("Welcome to Ruvents");
        home.ShouldContain("fluent-layout");

        using var unauthorized = await client.GetAsync(new Uri("/api/account/summary", UriKind.Relative), timeout.Token);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
