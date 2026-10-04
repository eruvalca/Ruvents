using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace Ruvents.Testing;

/// <summary>Creates isolated copies of the real AppHost for HTTP and browser tests.</summary>
public static class TestAppHost
{
    public static async Task<IDistributedApplicationTestingBuilder> CreateAsync(CancellationToken cancellationToken)
        => await CreateAsync(enableOfflineFallback: false, cancellationToken);

    public static async Task<IDistributedApplicationTestingBuilder> CreateAsync(bool enableOfflineFallback, CancellationToken cancellationToken)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Ruvents_AppHost>(
            ["--environment=Development"], cancellationToken);
        try
        {
            // Never attach tests to development data. Preserve the real resource graph,
            // health checks and migration dependencies, but use disposable storage.
            foreach (var container in builder.Resources.OfType<ContainerResource>())
            {
                foreach (var mount in container.Annotations.OfType<ContainerMountAnnotation>().ToArray())
                {
                    container.Annotations.Remove(mount);
                }

                builder.CreateResourceBuilder(container).WithLifetime(ContainerLifetime.Session);
            }

            if (enableOfflineFallback)
            {
                var web = builder.Resources.OfType<ProjectResource>().Single(resource => string.Equals(resource.Name, "ruvents", StringComparison.Ordinal));
                // Keep Aspire's environment/connection-string configuration intact.
                builder.CreateResourceBuilder(web).WithArgs("--Pwa:EnableOfflineFallback=true");
            }

            return builder;
        }
        catch
        {
            await builder.DisposeAsync();
            throw;
        }
    }
}
