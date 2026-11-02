using Microsoft.AspNetCore.Http;

namespace Ruvents.Features.Account.Services;

internal sealed class HttpFormPhotoReader : IProfilePhotoUploadReader
{
    private const string FieldName = "Photo";

    public IFormFile? Read(HttpContext httpContext) =>
        httpContext.Request.HasFormContentType
            ? httpContext.Request.Form.Files.GetFile(FieldName)
            : null;
}
