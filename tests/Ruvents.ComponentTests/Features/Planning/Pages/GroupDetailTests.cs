using System.Diagnostics.CodeAnalysis;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Ruvents.SharedKernel.Events;
using Ruvents.SharedKernel.Families;
using Ruvents.SharedKernel.Planning;
using Ruvents.UI.Planning;
using Shouldly;
using Xunit;
using GroupDetailPage = Ruvents.UI.Features.Planning.Pages.GroupDetail;

namespace Ruvents.ComponentTests.Features.Planning.Pages;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class GroupDetailTests
{
    private static readonly Guid _groupId = Guid.Parse("6f0b5a2e-1a4c-4a70-9c3e-2b7f1c8d9e01");
    private static readonly DateTimeOffset _now = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static readonly PlanningFamilyDetail _group = new(
        _groupId,
        "Ruvalcaba Family Mega Group",
        "The Big Kahuna",
        FamilyRole.Owner,
        [new PlanningFamilyMember(Guid.Parse("9c1d2e3f-4a5b-4c6d-8e7f-0a1b2c3d4e5f"), "user-1", "eruvalca", FamilyRole.Owner, _now.AddYears(-1), false)],
        []);

    [Fact]
    public async Task FinishedEventsMoveToThePastSectionWithAChipAsync()
    {
        await using var context = CreateContext(
            UpcomingEvent("Winter Potluck", _now.AddDays(20)),
            PastEvent("Peach Cobbler Factory", _now.AddDays(-1)));

        var component = Render(context);

        var events = Section(component, "Events");
        events.TextContent.ShouldContain("Winter Potluck");
        events.TextContent.ShouldNotContain("Peach Cobbler Factory");

        var past = Section(component, "Past events");
        past.TextContent.ShouldContain("Peach Cobbler Factory");
        var card = past.QuerySelector("li.is-past");
        card.ShouldNotBeNull();
        card.QuerySelector(".chip")!.TextContent.Trim().ShouldBe("Past");
    }

    [Fact]
    public async Task UpcomingReadsSoonestFirstAndPastReadsMostRecentFirstAsync()
    {
        await using var context = CreateContext(
            UpcomingEvent("Later up", _now.AddDays(40)),
            UpcomingEvent("Sooner up", _now.AddDays(2)),
            PastEvent("Long ago", _now.AddMonths(-3)),
            PastEvent("Yesterday", _now.AddDays(-1)));

        var component = Render(context);

        string.Join(", ", Titles(Section(component, "Events"))).ShouldBe("Sooner up, Later up");
        string.Join(", ", Titles(Section(component, "Past events"))).ShouldBe("Yesterday, Long ago");
    }

    [Fact]
    public async Task ScheduledEventInItsWindowShowsHappeningNowAsync()
    {
        await using var context = CreateContext(
            Summary("Live", _now.AddHours(-1), _now.AddHours(1), EventStatus.Scheduled));

        var events = Section(Render(context), "Events");

        events.TextContent.ShouldContain("Happening now");
    }

    [Fact]
    public async Task CancelledEventDoesNotClaimToBeHappeningNowAsync()
    {
        await using var context = CreateContext(
            Summary("Called off", _now.AddHours(-1), _now.AddHours(1), EventStatus.Cancelled));

        var events = Section(Render(context), "Events");

        events.TextContent.ShouldContain("Cancelled");
        events.TextContent.ShouldNotContain("Happening now");
    }

    [Fact]
    public async Task ProvisionalViewerZoneIsConfirmedAndTheCardRerendersAsync()
    {
        await using var context = CreateContext(
            new ConfirmingViewerTimeZone("UTC", "Asia/Tokyo"),
            UpcomingEvent("Winter Potluck", _now.AddDays(20)));

        var component = Render(context);

        // The first render uses the provisional zone; confirming with the browser moves
        // the card to Tokyo time and adds the event-zone note.
        await component.WaitForAssertionAsync(() =>
        {
            var events = Section(component, "Events");
            events.TextContent.ShouldContain("9:00 PM");
            events.TextContent.ShouldContain("Event time");
            events.TextContent.ShouldContain("12:00 PM");
        });
    }

    [Fact]
    public async Task PastEventsPreviewSixAndTheToggleRevealsAndHidesTheRestAsync()
    {
        await using var context = CreateContext(
            Enumerable.Range(0, 8)
                .Select(index => PastEvent($"Past {index}", _now.AddDays(-(index + 1))))
                .ToArray());

        var component = Render(context);

        PastCards(component).Count.ShouldBe(6);
        PastToggle(component).TextContent.Trim().ShouldBe("Show all 8 past events");

        await PastToggle(component).ClickAsync();
        await component.WaitForAssertionAsync(() =>
        {
            PastCards(component).Count.ShouldBe(8);
            PastToggle(component).TextContent.Trim().ShouldBe("Show fewer past events");
        });

        await PastToggle(component).ClickAsync();
        await component.WaitForAssertionAsync(() =>
        {
            PastCards(component).Count.ShouldBe(6);
            PastToggle(component).TextContent.Trim().ShouldBe("Show all 8 past events");
        });
    }

    [Fact]
    public async Task SixPastEventsFitThePreviewWithoutAToggleAsync()
    {
        await using var context = CreateContext(
            Enumerable.Range(0, 6)
                .Select(index => PastEvent($"Past {index}", _now.AddDays(-(index + 1))))
                .ToArray());

        var component = Render(context);
        var past = Section(component, "Past events");

        PastCards(component).Count.ShouldBe(6);
        past.QuerySelector("button").ShouldBeNull();
    }

    private static BunitContext CreateContext(params PlanningEventSummary[] events) =>
        CreateContext(new UnresolvedViewerTimeZone(), events);

    private static BunitContext CreateContext(IViewerTimeZone viewerTimeZone, params PlanningEventSummary[] events)
    {
        var context = new BunitContext();
        var api = Substitute.For<IPlanningApi>();
        api.GetFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PlanningFamilyDetail?>(_group));
        api.ListGroupEventsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PlanningEventSummary>>(events));
        context.Services.AddSingleton(api);
        context.Services.AddSingleton(viewerTimeZone);
        context.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(_now));
        context.Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        return context;
    }

    private static IRenderedComponent<GroupDetailPage> Render(BunitContext context) =>
        context.Render<GroupDetailPage>(parameters => parameters.Add(page => page.GroupId, _groupId));

    private static AngleSharp.Dom.IElement Section(IRenderedComponent<GroupDetailPage> component, string heading) =>
        component.FindAll("section").Single(section => string.Equals(section.QuerySelector("h2")?.TextContent, heading, StringComparison.Ordinal));

    private static string[] Titles(AngleSharp.Dom.IElement section) =>
        section.QuerySelectorAll("a.event-title").Select(link => link.TextContent).ToArray();

    private static List<AngleSharp.Dom.IElement> PastCards(IRenderedComponent<GroupDetailPage> component) =>
        Section(component, "Past events").QuerySelectorAll("li.is-past").ToList();

    private static AngleSharp.Dom.IElement PastToggle(IRenderedComponent<GroupDetailPage> component) =>
        Section(component, "Past events").QuerySelector("button")!;

    private static PlanningEventSummary UpcomingEvent(string title, DateTimeOffset start) =>
        Summary(title, start, start.AddHours(2), EventStatus.Scheduled);

    private static PlanningEventSummary PastEvent(string title, DateTimeOffset start) =>
        Summary(title, start, start.AddHours(2), EventStatus.Scheduled);

    private static PlanningEventSummary Summary(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        EventStatus status) =>
        new(Guid.NewGuid(), _groupId, title, start, end, false, "UTC", "Somewhere", status, false);

    private sealed class UnresolvedViewerTimeZone : IViewerTimeZone
    {
        public string? ResolvedZoneId => null;

        public bool IsConfirmed => false;

        public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<string?>(null);
    }

    private sealed class ConfirmingViewerTimeZone(string provisionalZoneId, string confirmedZoneId) : IViewerTimeZone
    {
        public string? ResolvedZoneId { get; private set; } = provisionalZoneId;

        public bool IsConfirmed { get; private set; }

        public ValueTask<string?> ResolveAsync(CancellationToken cancellationToken)
        {
            ResolvedZoneId = confirmedZoneId;
            IsConfirmed = true;
            return ValueTask.FromResult<string?>(ResolvedZoneId);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
