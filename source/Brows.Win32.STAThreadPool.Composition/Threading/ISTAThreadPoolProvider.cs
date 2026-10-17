using Brows.Composition;

namespace Brows.Threading;

/// <summary>
/// Provides STA thread pools for target objects.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IExport"/>, so the composition framework discovers and creates
/// a single instance of each implementation for the lifetime of the program.
/// Implementations choose how targets are mapped to STA thread pool instances and whether pools
/// are shared or created per target. The provider's composition lifetime does not prescribe the
/// lifetime of the pools it returns.
/// This contract is part of the platform-neutral <c>Brows.Win32.STAThreadPool.Composition</c> package.
/// No provider implementation is supplied; the <c>Brows.Win32.STAThreadPool.Composition.Exports</c>
/// package supplies only a factory for creating Windows STA thread pools.
/// </remarks>
public interface ISTAThreadPoolProvider : IExport {
    /// <summary>
    /// Gets the STA thread pool associated with the specified target.
    /// </summary>
    /// <param name="target">
    /// Object used to identify or resolve the thread pool.
    /// </param>
    /// <returns>
    /// The STA thread pool for the specified target.
    /// </returns>
    public ISTAThreadPool GetThreadPool(object target);
}
