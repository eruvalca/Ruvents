using System.Diagnostics.CodeAnalysis;
using Ruvents.Features.Account.Extensions;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account.Extensions;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class AuthenticatorKeyExtensionsTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("ABC", "abc")]
    [InlineData("ABCD", "abcd")]
    [InlineData("ABCDE", "abcd e")]
    [InlineData("ABCDEFG", "abcd efg")]
    [InlineData("ABCDEFGH", "abcd efgh")]
    [InlineData("ABCDEFGHI", "abcd efgh i")]
    [InlineData("ABcdEF12GHIJ", "abcd ef12 ghij")]
    public void FormattingLowercasesAndGroupsWithoutLeadingOrTrailingSpaces(string key, string expected)
    {
        ArgumentNullException.ThrowIfNull(key);

        key.FormatAuthenticatorKey().ShouldBe(expected);
    }
}
