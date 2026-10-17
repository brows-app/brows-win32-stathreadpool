using Brows.Composition;

namespace Brows.Threading;

/// <summary>
/// Defines a factory that creates instances of <see cref="ISTAThreadPool"/>.
/// </summary>
/// <remarks>
/// This interface extends <c>Brows.Composition.IExport</c>, so an implementation is discovered and
/// instantiated by the composition framework as a single instance for the lifetime of the program.
/// This interface is part of the platform-neutral <c>Brows.Win32.STAThreadPool.Composition</c> package;
/// implementations are provided separately, such as the one in the
/// <c>Brows.Win32.STAThreadPool.Composition.Exports</c> package, whose factory creates the Windows
/// <c>STAThreadPool</c> from the <c>Brows.Win32.STAThreadPool</c> package and therefore requires Windows.
/// </remarks>
public interface ISTAThreadPoolFactory : IExport {
    /// <summary>
    /// Creates a new instance of <see cref="ISTAThreadPool"/>.
    /// </summary>
    /// <param name="name">
    /// The name used to identify the pool and its worker threads.
    /// </param>
    /// <param name="options">
    /// The worker retention and scheduling options to copy for the pool, or <see langword="null"/> to use defaults.
    /// </param>
    /// <returns>
    /// A new <see cref="ISTAThreadPool"/> with the given <paramref name="name"/> and
    /// <paramref name="options"/>. Each call creates a separate pool; the implementor's lifetime is
    /// managed by the composition framework, but the pools it creates are not retained by it.
    /// </returns>
    ISTAThreadPool CreateThreadPool(string name, STAThreadPoolOptions options = null);
}
