using System.Diagnostics.CodeAnalysis;
using Ruvents.Features.Account.Models;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "xUnit requires public test classes for discovery.")]
public sealed class TokenDecodeOutcomeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    [InlineData("%%%")]
    [InlineData("A===")]
    public void MissingAndMalformedEncodingReturnsInvalidToken(string? encoded) =>
        TokenDecodeOutcome.Decode(encoded).Value.ShouldBeOfType<TokenDecodeOutcome.InvalidToken>();

    [Theory]
    [InlineData("", "")]
    [InlineData("YWJj", "abc")]
    [InlineData("dG9rZW4rL8Op", "token+/é")]
    public void ValidEncodingPreservesDecodedValueIncludingEmptyForIdentityValidation(string encoded, string expected) =>
        TokenDecodeOutcome.Decode(encoded).Value.ShouldBeOfType<TokenDecodeOutcome.DecodedToken>().Value.ShouldBe(expected);
}
