using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Ruvents.Features.Account.Extensions;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account.Extensions;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class HttpRequestExtensionsTests
{
    [Theory]
    [InlineData("GET", true)]
    [InlineData("get", true)]
    [InlineData("gEt", true)]
    [InlineData("POST", false)]
    [InlineData("HEAD", false)]
    [InlineData("", false)]
    [InlineData(" GET", false)]
    public void IsGetMatchesHttpSemantics(string method, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;

        context.Request.IsGet.ShouldBe(expected);
    }
}
