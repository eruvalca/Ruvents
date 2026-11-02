using System.Diagnostics.CodeAnalysis;
using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Ruvents.Features.Account.Pages.Manage;
using Shouldly;
using Xunit;

namespace Ruvents.ComponentTests.Features.Account;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "xUnit requires public test classes for discovery.")]
public sealed class ProfilePhotoManagementTests
{
    [Fact]
    public async Task CurrentPhotoIsShownWithAnUploadControlAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        var user = account.Authenticate();
        user.Id = "member-42";
        user.ProfilePhotoBlobName = "member-42/photo.jpg";
        account.Http.Request.Method = HttpMethods.Get;

        var component = account.Render<Photo>(context);

        component.Find("img.avatar-preview").GetAttribute("src").ShouldBe("/account/photo/member-42");
        component.Find("img.avatar-preview").GetAttribute("alt").ShouldBe("Your current profile photo");
        // The cropper input, plus the no-JavaScript file fallback inside <noscript>.
        component.FindAll("#crop-photo").Count.ShouldBe(1);
        component.FindAll("noscript input#Photo").Count.ShouldBe(1);
        // Nothing can be saved until the user has produced a crop, so the only
        // submit button is the one inside the no-JavaScript fallback.
        component.FindAll("button[type='submit']").Count.ShouldBe(1);
        component.FindAll("noscript button[type='submit']").Count.ShouldBe(1);
        await account.Photos.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AccountWithoutPhotoOffersThePickerWithoutAPreviewAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        var user = account.Authenticate();
        user.Id = "member-42";
        account.Http.Request.Method = HttpMethods.Get;

        var component = account.Render<Photo>(context);

        component.FindAll("img.avatar-preview").ShouldBeEmpty();
        component.FindAll("#crop-photo").Count.ShouldBe(1);
        component.FindAll("button[type='submit']").Count.ShouldBe(1);
    }

    [Fact]
    public async Task CroppedUploadReplacesThePhotoAndConfirmsWithoutAnotherPostAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        var user = account.Authenticate();
        user.Id = "member-42";
        user.ProfilePhotoBlobName = "member-42/old.png";
        account.Http.Request.Method = HttpMethods.Post;
        account.Http.Request.ContentType = "application/x-www-form-urlencoded";
        account.Http.Request.Form = new FormCollection(new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["PhotoData"] = "data:image/jpeg;base64,AQID",
        });

        account.Render<Photo>(context);

        await account.Photos.Received(1).SaveAsync("member-42", Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>());
        await account.Photos.Received(1).DeleteAsync("member-42/old.png", Arg.Any<CancellationToken>());
        await account.Users.Received(1).UpdateAsync(user);
        await account.SignIn.Received(1).RefreshSignInAsync(user);
        user.ProfilePhotoContentType.ShouldBe("image/jpeg");
        account.StatusCookie.ShouldContain("Your profile photo has been updated");
    }

    [Fact]
    public async Task SubmissionWithoutAChosenPhotoReportsAnErrorAndWritesNothingAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        var user = account.Authenticate();
        user.Id = "member-42";
        account.Http.Request.Method = HttpMethods.Post;
        account.Http.Request.ContentType = "application/x-www-form-urlencoded";
        account.Http.Request.Form = new FormCollection(new Dictionary<string, StringValues>(StringComparer.Ordinal));
        account.Uploads.Read(Arg.Any<HttpContext>()).Returns((IFormFile?)null);

        var component = account.Render<Photo>(context);

        var notice = component.Find(".notice[data-kind='error']");
        notice.TextContent.ShouldContain("Choose a profile photo");
        await account.Photos.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!, Xunit.TestContext.Current.CancellationToken);
        await account.Users.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        await account.SignIn.DidNotReceiveWithAnyArgs().RefreshSignInAsync(default!);
    }

    [Fact]
    public async Task OversizedUploadIsRejectedBeforeTheStreamIsReadAsync()
    {
        await using var context = new BunitContext();
        var account = context.ConfigureInteractiveAccount();
        var user = account.Authenticate();
        user.Id = "member-42";
        account.Http.Request.Method = HttpMethods.Post;
        account.Http.Request.ContentType = "application/x-www-form-urlencoded";
        account.Http.Request.Form = new FormCollection(new Dictionary<string, StringValues>(StringComparer.Ordinal));
        var file = Substitute.For<IFormFile>();
        file.ContentType.Returns("image/jpeg");
        file.Length.Returns(5 * 1024 * 1024);
        account.Uploads.Read(Arg.Any<HttpContext>()).Returns(file);
        account.Photos.TryValidate(Arg.Any<string?>(), Arg.Any<long>(), out Arg.Any<string>())
            .Returns(call =>
            {
                call[2] = "The profile photo must be 2 MB or smaller.";
                return false;
            });

        var component = account.Render<Photo>(context);

        component.Find(".notice[data-kind='error']").TextContent.ShouldContain("2 MB or smaller");
        file.DidNotReceive().OpenReadStream();
    }
}
