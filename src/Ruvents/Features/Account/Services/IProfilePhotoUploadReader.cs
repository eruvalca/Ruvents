using Microsoft.AspNetCore.Http;

namespace Ruvents.Features.Account.Services;

/// <summary>
/// Reads the uploaded profile photo. Isolating the raw form keeps the pages testable
/// and keeps multipart parsing in one place.
/// </summary>
internal interface IProfilePhotoUploadReader
{
    IFormFile? Read(HttpContext httpContext);
}
