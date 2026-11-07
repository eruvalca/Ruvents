using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Ruvents.Data;
using Ruvents.Features.Account.Services;

namespace Ruvents.Features.Account.Pages.Manage;

public sealed partial class Photo
{
    private ApplicationUser? _user;
    private string? _error;
    private string? _photoPreview;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [Inject]
    private ProfilePhotoWriter PhotoWriter { get; set; } = default!;

    private bool HasPhoto => !string.IsNullOrEmpty(_user?.ProfilePhotoBlobName);

    private string PhotoUrl => ProfilePhotoService.UrlFor(_user?.Id);

    protected override async Task OnInitializedAsync()
    {
        _user = await UserManager.GetUserAsync(HttpContext.User);
        if (_user is null)
        {
            RedirectManager.RedirectToInvalidUser(UserManager, HttpContext);
            return;
        }

        if (!HttpMethods.IsPost(HttpContext.Request.Method))
        {
            return;
        }

        _photoPreview = ProfilePhotoWriter.ReadPostedPreview(HttpContext);
        _error = await PhotoWriter.TryReplaceAsync(_user, HttpContext, HttpContext.RequestAborted);
        if (_error is not null)
        {
            return;
        }

        // Redirect after the write so a refresh cannot resubmit the upload.
        RedirectManager.RedirectToCurrentPageWithStatus("Your profile photo has been updated", HttpContext);
    }
}
