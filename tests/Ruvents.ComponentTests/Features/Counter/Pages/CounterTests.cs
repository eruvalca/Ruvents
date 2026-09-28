using System.Diagnostics.CodeAnalysis;
using Bunit;
using Shouldly;
using Xunit;
using CounterPage = Ruvents.UI.Features.Counter.Pages.Counter;

namespace Ruvents.ComponentTests.Features.Counter.Pages;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class CounterTests
{
    [Fact]
    public async Task RenderShowsInitialCountAndIncrementButtonAsync()
    {
        await using var context = new BunitContext();

        var component = context.Render<CounterPage>();

        var differences = component.CompareTo(
            """
            <h1>Counter</h1>
            <p role="status">Current count: 0</p>
            <button>Click me</button>
            """);

        differences.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1, "Current count: 1")]
    [InlineData(2, "Current count: 2")]
    [InlineData(5, "Current count: 5")]
    public async Task ClickingIncrementButtonUpdatesDisplayedCountAsync(int clicks, string expectedStatus)
    {
        await using var context = new BunitContext();
        var component = context.Render<CounterPage>();

        for (var click = 0; click < clicks; click++)
        {
            await component.Find("button").ClickAsync();
        }

        await component.WaitForAssertionAsync(() => component.Find("[role='status']").TextContent.ShouldBe(expectedStatus));
    }

    [Fact]
    public async Task IndependentContextsKeepCounterStateIsolatedAsync()
    {
        await using var firstContext = new BunitContext();
        await using var secondContext = new BunitContext();
        var first = firstContext.Render<CounterPage>();
        var second = secondContext.Render<CounterPage>();

        await first.Find("button").ClickAsync();

        await first.WaitForAssertionAsync(() => first.Find("[role='status']").TextContent.ShouldBe("Current count: 1"));
        second.Find("[role='status']").TextContent.ShouldBe("Current count: 0");
    }
}
