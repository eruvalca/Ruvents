using Cropper.Blazor.Components;
using Cropper.Blazor.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Ruvents.Features.Account.Services;
using Ruvents.UI.Components;

namespace Ruvents.Features.Account.Components;

/// <summary>
/// Picks a photo and lets the user position it inside a square crop for a circular
/// avatar. The cropped result travels to the server as a hidden field on the
/// containing form, so registration keeps its normal static post.
/// </summary>
public sealed partial class PhotoCropField : CancellableComponentBase
{
    private const int OutputSize = 320;
    private const float OutputQuality = 0.85f;
    private const int MaximumChunkSize = 64 * 1024;

    private static readonly Options _options = new() { AspectRatio = 1m, AutoCropArea = 1m };

    /// <summary>
    /// Label for the submit button rendered once a crop exists. When null, the
    /// containing page owns the form's submit button, so capturing a crop never
    /// posts the form by itself.
    /// </summary>
    [Parameter]
    public string? SubmitLabel { get; set; }

    [Inject]
    private IProfilePhotoStore Photos { get; set; } = default!;

    private CropperComponent? _cropper;
    private string? _source;
    private string? _preview;
    private string? _error;

    private async Task OnFileSelectedAsync(InputFileChangeEventArgs args)
    {
        _error = null;
        _preview = null;

        using var operation = CreateOperation();
        var file = args.File;
        if (!Photos.TryValidate(file.ContentType, file.Size, out var error))
        {
            _error = error;
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(ProfilePhotoService.MaxBytes, operation.Token);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, operation.Token);
            _source = $"data:{file.ContentType};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (IOException)
        {
            _error = "That image could not be read. Choose a smaller file.";
        }
    }

    private async Task CaptureAsync()
    {
        if (_cropper is null || _source is null)
        {
            _error = "Choose a photo first.";
            return;
        }

        using var operation = CreateOperation();
        try
        {
            var receiver = await _cropper.GetCroppedCanvasDataInBackgroundAsync(
                new GetCroppedCanvasOptions
                {
                    Width = OutputSize,
                    Height = OutputSize,
                    MinWidth = OutputSize,
                    MinHeight = OutputSize,
                    MaxWidth = OutputSize,
                    MaxHeight = OutputSize,
                },
                "image/jpeg",
                OutputQuality,
                MaximumChunkSize,
                operation.Token);

            using var buffer = new MemoryStream();
            await foreach (var chunk in receiver.GetImageStreamAsync(operation.Token).WithCancellation(operation.Token))
            {
                await buffer.WriteAsync(chunk, operation.Token);
            }

            var bytes = buffer.ToArray();
            if (bytes.Length == 0)
            {
                _error = "The photo could not be cropped. Choose it again.";
                return;
            }

            _preview = $"data:image/jpeg;base64,{Convert.ToBase64String(bytes)}";
            _error = null;
        }
        catch (JSException)
        {
            _error = "The photo could not be cropped in this browser.";
        }
    }
}
