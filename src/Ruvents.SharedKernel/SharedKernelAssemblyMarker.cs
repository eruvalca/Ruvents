using System.Diagnostics.CodeAnalysis;

namespace Ruvents.SharedKernel;

/// <summary>
/// Identifies the shared kernel assembly for architecture tests.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094:Classes should not be empty",
    Justification = "Identifies the shared kernel assembly through typeof(SharedKernelAssemblyMarker).Assembly.")]
public static class SharedKernelAssemblyMarker
{
}
