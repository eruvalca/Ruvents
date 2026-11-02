using System.Diagnostics.CodeAnalysis;
using Ruvents.Features.Account.Services;
using Shouldly;
using Xunit;

namespace Ruvents.UnitTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class ProfilePhotoDataUrlTests
{
    [Fact]
    public void CroppedPhotoDecodesBytesAndContentType()
    {
        var payload = $"data:image/jpeg;base64,{Convert.ToBase64String([1, 2, 3, 4])}";

        var decoded = ProfilePhotoService.TryDecodeDataUrl(payload, out var bytes, out var contentType, out var error);

        decoded.ShouldBeTrue(error);
        bytes.ShouldBe([1, 2, 3, 4]);
        contentType.ShouldBe("image/jpeg");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("image/png;base64,AAAA")]
    public void PayloadWithoutDataPrefixIsRejected(string payload)
    {
        var decoded = ProfilePhotoService.TryDecodeDataUrl(payload, out var bytes, out _, out var error);

        decoded.ShouldBeFalse();
        bytes.ShouldBeEmpty();
        error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PayloadWithoutBase64MarkerIsRejected()
    {
        var decoded = ProfilePhotoService.TryDecodeDataUrl("data:image/png,AAAA", out _, out _, out var error);

        decoded.ShouldBeFalse();
        error.ShouldContain("cropped photo");
    }

    [Fact]
    public void InvalidBase64IsRejected()
    {
        var decoded = ProfilePhotoService.TryDecodeDataUrl("data:image/png;base64,not base64!", out _, out _, out var error);

        decoded.ShouldBeFalse();
        error.ShouldContain("cropped photo");
    }
}
