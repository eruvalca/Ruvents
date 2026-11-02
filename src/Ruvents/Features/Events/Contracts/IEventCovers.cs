namespace Ruvents.Features.Events.Contracts;

internal interface IEventCovers
{
    bool TryValidate(string? contentType, long length, out string error);

    Task<bool> SaveAsync(Guid eventId, string userId, Stream content, string contentType, CancellationToken cancellationToken);

    Task<(Stream Content, string ContentType)?> OpenAsync(Guid eventId, string userId, CancellationToken cancellationToken);
}
