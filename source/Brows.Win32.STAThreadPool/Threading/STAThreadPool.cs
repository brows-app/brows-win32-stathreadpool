using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading;

/// <summary>
/// Provides a Windows implementation of a reusable single-threaded apartment thread pool.
/// </summary>
/// <remarks>
/// Worker threads use a Windows Forms message loop, so this implementation requires Windows. On .NET 8
/// and later, consuming projects must target a Windows TFM, such as <c>net8.0-windows</c>. The platform-neutral
/// contract types are available separately in the <c>Brows.Win32.STAThreadPool.Contracts</c> package.
/// </remarks>
public sealed class STAThreadPool : ISTAThreadPool {
    private static readonly ILog Log = Logging.For(typeof(STAThreadPool));

    private readonly List<STAThreadWorker> Workers = [];
    private readonly
#if NET9_0_OR_GREATER
        Lock
#else
        object
#endif
    TimerLock = new();

    private long WorkerID;
    private Timer Timer;

    internal TimeSpan TimerPeriod { get; set; } = TimeSpan.FromMinutes(1);

    internal int WorkerCount {
        get {
            lock (Workers) {
                return Workers.Count;
            }
        }
    }

    private void TimerStart() {
        lock (TimerLock) {
            if (Timer is not null) {
                return;
            }
            Timer = new Timer(TimerCallback, null, TimerPeriod, Timeout.InfiniteTimeSpan);
        }
    }

    private void TimerCallback(object _) {
        if (Log.Info()) {
            Log.Info(nameof(TimerCallback));
        }
        lock (TimerLock) {
            Timer?.Change(Timeout.Infinite, Timeout.Infinite);
            Timer?.Dispose();
            Timer = null;
        }
        lock (Workers) {
            var workerCountMin = WorkerCountMin;
            var workerCount = Workers.Count;
            if (workerCount <= workerCountMin) {
                return;
            }
            var idle = default(List<STAThreadWorker>);
            var removable = workerCount - workerCountMin;
            foreach (var worker in Workers) {
                if (removable == 0) {
                    break;
                }
                if (worker.Working == false) {
                    var idleTime = worker.IdleTime;
                    if (idleTime.HasValue) {
                        if (idleTime.Value > IdleTime) {
                            idle ??= [];
                            idle.Add(worker);
                            removable--;
                        }
                    }
                }
            }
            if (idle is not null) {
                foreach (var worker in idle) {
                    worker.Exit();
                    Workers.Remove(worker);
                }
            }
            if (Workers.Count > workerCountMin) {
                TimerStart();
            }
        }
    }

    private async Task<(bool worked, TResult result)> TryWork<TResult>(STAThreadWorkItem<TResult> item,
                                                                       CancellationToken cancellationToken) {
        /*
         * Return without selecting or creating a worker when the token is already canceled, so a
         * canceled Work call does not leave behind a worker that was never used. DoWork surfaces
         * the cancellation through the delayed retry below.
         */
        if (cancellationToken.IsCancellationRequested) {
            return (worked: false, result: default);
        }
        var worker = default(STAThreadWorker);
        lock (Workers) {
            var idle = Workers.FirstOrDefault(w => w.Working == false);
            if (idle is null && WorkerCountMax > Workers.Count) {
                idle = new STAThreadWorker(Name, ++WorkerID);
                Workers.Add(idle);
            }
            if (idle is null) {
                return (worked: false, result: default);
            }
            worker = idle;
            worker.Working = true;
        }
        try {
            return (worked: true, result: await worker.Work(item, cancellationToken));
        }
        finally {
            lock (Workers) {
                worker.Working = false;
                worker.ExitPending();
                if (Workers.Count > WorkerCountMin) {
                    TimerStart();
                }
            }
        }
    }

    private async Task<TResult> DoWork<TResult>(STAThreadWorkItem<TResult> item,
                                                CancellationToken cancellationToken) {
        for (; ; ) {
            var (worked, result) = await TryWork(item, cancellationToken);
            if (worked) {
                return result;
            }
            var tryWorkDelay = TryWorkDelay;
            if (tryWorkDelay > 0) {
                await Task.Delay(tryWorkDelay, cancellationToken);
            }
        }
    }

    private async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item,
                                              CancellationToken cancellationToken) {
        return await DoWork(item, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the length of time an inactive worker may be retained before it is removed.
    /// The default is 2.5 minutes.
    /// </summary>
    public TimeSpan IdleTime {
        get => Options.IdleTime;
        set => Options.IdleTime = value;
    }

    /// <summary>
    /// Gets or sets the delay, in milliseconds, before retrying when all workers are busy.
    /// The value must be 1 or greater. The default is 10 milliseconds.
    /// </summary>
    public int TryWorkDelay {
        get => Options.TryWorkDelay;
        set => Options.TryWorkDelay = value;
    }

    /// <summary>
    /// Gets or sets the maximum number of workers that can be created. The value must be 1 or greater
    /// and cannot be less than <see cref="WorkerCountMin"/>. The default is
    /// <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int WorkerCountMax {
        get => Options.WorkerCountMax;
        set => Options.WorkerCountMax = value;
    }

    /// <summary>
    /// Gets or sets the minimum number of idle workers retained by the pool. The value must be 0 or greater
    /// and cannot exceed <see cref="WorkerCountMax"/>. The default is 1.
    /// </summary>
    public int WorkerCountMin {
        get => Options.WorkerCountMin;
        set => Options.WorkerCountMin = value;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Gets the worker retention and scheduling options used by this pool.
    /// </summary>
    /// <remarks>
    /// This instance is a copy of the options supplied to the constructor, or a new instance with default values.
    /// Changes to its properties update this pool's configuration.
    /// </remarks>
    public STAThreadPoolOptions Options { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="STAThreadPool"/> class.
    /// </summary>
    /// <param name="name">
    /// The name used to identify the pool and its worker threads.
    /// </param>
    /// <param name="options">
    /// The worker retention and scheduling options to copy for this pool, or <see langword="null"/> to use defaults.
    /// </param>
    public STAThreadPool(string name, STAThreadPoolOptions options = null) {
        Name = name;
        Options = (options ?? new()) with { };
    }

    /// <inheritdoc/>
    Task<TResult> ISTAThreadPool.Work<TResult>(string name,
                                               Func<TResult> work,
                                               CancellationToken cancellationToken) {
        return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
    }

    /// <inheritdoc/>
    Task<TResult> ISTAThreadPool.Work<TResult>(string name,
                                             Func<CancellationToken, Task<TResult>> work,
                                             CancellationToken cancellationToken) {
        return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
    }

    /// <inheritdoc/>
    async Task ISTAThreadPool.Work(string name, Action work, CancellationToken cancellationToken) {
        await ((ISTAThreadPool)this).Work<object>(name, () => {
            if (work is not null) {
                work();
            }
            return default;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    async Task ISTAThreadPool.Work(string name,
                                   Func<CancellationToken, Task> work,
                                   CancellationToken cancellationToken) {
        await ((ISTAThreadPool)this).Work<object>(name, async token => {
            if (work is not null) {
                await work(token);
            }
            return default;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    void ISTAThreadPool.Empty() {
        if (Log.Info()) {
            Log.Info(nameof(ISTAThreadPool.Empty));
        }
        lock (Workers) {
            var snapshot = new List<STAThreadWorker>(Workers);
            foreach (var worker in snapshot) {
                worker.Exit();
                Workers.Remove(worker);
            }
        }
    }
}
