namespace Brows.Threading;

/// <summary>
/// Provides STA thread pools for target objects.
/// </summary>
/// <remarks>Implementations can choose how targets are mapped to STA thread pool instances and whether pools are
/// shared or created per target.</remarks>
public interface ISTAThreadPoolProvider {
    /// <summary>
    /// Gets the STA thread pool associated with the specified target.
    /// </summary>
    /// <param name="target">Object used to identify or resolve the thread pool.</param>
    /// <returns>The STA thread pool for the specified target.</returns>
    public ISTAThreadPool GetThreadPool(object target);
}
