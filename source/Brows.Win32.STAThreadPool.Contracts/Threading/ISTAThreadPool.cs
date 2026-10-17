using System;
using System.Threading.Tasks;

namespace Brows.Threading;

/// <summary>
/// Defines the operations exposed by a reusable pool of single-threaded apartment threads.
/// </summary>
/// <remarks>
/// This interface is part of the platform-neutral <c>Brows.Win32.STAThreadPool.Contracts</c> package.
/// Referencing the contracts package alone does not require a Windows runtime on cross-platform .NET
/// targets. An implementation may have additional platform requirements; <c>STAThreadPool</c> requires
/// Windows.
/// </remarks>
public interface ISTAThreadPool {
    /// <summary>
    /// Gets the length of time an inactive worker may be retained before it is removed.
    /// </summary>
    TimeSpan IdleTime { get; }

    /// <summary>
    /// Gets the name used to identify this pool and its worker threads.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the delay, in milliseconds, before retrying when all workers are busy. The value must be 1 or greater.
    /// </summary>
    int TryWorkDelay { get; }

    /// <summary>
    /// Gets the maximum number of workers that can be created. The value must be 1 or greater
    /// and cannot be less than <see cref="WorkerCountMin"/>.
    /// </summary>
    int WorkerCountMax { get; }

    /// <summary>
    /// Gets the minimum number of idle workers retained by the pool. The value must be 0 or greater
    /// and cannot exceed <see cref="WorkerCountMax"/>.
    /// </summary>
    int WorkerCountMin { get; }

    /// <summary>
    /// Removes all workers from the pool and requests that their STA message loops exit. A worker
    /// that is running work when this is called exits once that work has completed.
    /// </summary>
    void Empty();

    /// <summary>
    /// Runs synchronous work on an available STA worker.
    /// </summary>
    /// <param name="name">
    /// The name used to identify the work item.
    /// </param>
    /// <param name="work">
    /// The synchronous work to run.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the work.
    /// </param>
    /// <returns>
    /// A task that completes when <paramref name="work"/> has finished.
    /// </returns>
    Task Work(string name, Action work, CancellationToken cancellationToken);

    /// <summary>
    /// Runs asynchronous work on an available STA worker.
    /// </summary>
    /// <param name="name">
    /// The name used to identify the work item.
    /// </param>
    /// <param name="work">
    /// The asynchronous work to run.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the work.
    /// </param>
    /// <returns>
    /// A task that completes when <paramref name="work"/> has finished.
    /// </returns>
    Task Work(string name, Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    /// <summary>
    /// Runs asynchronous work on an available STA worker.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type returned by <paramref name="work"/>.
    /// </typeparam>
    /// <param name="name">
    /// The name used to identify the work item.
    /// </param>
    /// <param name="work">
    /// The asynchronous work to run.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the work.
    /// </param>
    /// <returns>
    /// A task that completes with the result of <paramref name="work"/>.
    /// </returns>
    Task<TResult> Work<TResult>(string name, Func<CancellationToken, Task<TResult>> work, CancellationToken cancellationToken);

    /// <summary>
    /// Runs synchronous work on an available STA worker.
    /// </summary>
    /// <typeparam name="TResult">
    /// The type returned by <paramref name="work"/>.
    /// </typeparam>
    /// <param name="name">
    /// The name used to identify the work item.
    /// </param>
    /// <param name="work">
    /// The synchronous work to run.
    /// </param>
    /// <param name="cancellationToken">
    /// The token used to cancel the work.
    /// </param>
    /// <returns>
    /// A task that completes with the result of <paramref name="work"/>.
    /// </returns>
    Task<TResult> Work<TResult>(string name, Func<TResult> work, CancellationToken cancellationToken);
}
