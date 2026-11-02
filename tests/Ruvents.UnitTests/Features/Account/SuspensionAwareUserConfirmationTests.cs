using System.Diagnostics.CodeAnalysis;
using Ruvents.Data;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class SuspensionAwareUserConfirmationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SuspensionAloneDecidesWhetherTheAccountCountsAsConfirmedAsync(bool suspended, bool expected)
    {
        var user = new ApplicationUser { Email = "member@example.test" };
        if (suspended)
        {
            user.SuspendedAtUtc = DateTimeOffset.UnixEpoch;
        }

        // The implementation deliberately reads nothing from the manager, so it can be
        // null here without changing the behavior under test. The gate must not depend
        // on email confirmation, which this application never performs.
        var confirmed = await new SuspensionAwareUserConfirmation().IsConfirmedAsync(null!, user);

        confirmed.ShouldBe(expected);
    }
}
