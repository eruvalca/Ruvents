using System.Diagnostics.CodeAnalysis;
using Ruvents.Data;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class UserProfileNamesTests
{
    [Fact]
    public void DisplayNameTruncationDoesNotSplitSurrogatePairs()
    {
        var displayName = UserProfileNames.ResolveDisplayName(
            null,
            new string('a', 49),
            new string('b', 48),
            "𠮷田",
            string.Empty);

        // The cut lands on the high surrogate, so the pair and the trailing
        // separator before it are both dropped.
        displayName.Length.ShouldBe(98);
        displayName.ShouldBe(new string('a', 49) + " " + new string('b', 48));
        displayName.Any(char.IsSurrogate).ShouldBeFalse();
    }

    [Fact]
    public void DisplayNameTruncationDoesNotLeaveATrailingSeparator()
    {
        var displayName = UserProfileNames.ResolveDisplayName(
            null,
            new string('a', 49),
            new string('b', 49),
            "Lovelace",
            string.Empty);

        displayName.ShouldBe(new string('a', 49) + " " + new string('b', 49));
        displayName.Length.ShouldBe(99);
    }

    [Fact]
    public void DisplayNameTruncationKeepsTheFullBoundaryWhenNoPairIsSplit()
    {
        var displayName = UserProfileNames.ResolveDisplayName(
            null,
            new string('a', 60),
            null,
            new string('b', 60),
            string.Empty);

        displayName.Length.ShouldBe(100);
    }
}
