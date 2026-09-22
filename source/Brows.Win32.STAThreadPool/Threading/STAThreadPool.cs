using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading;

/// <summary>
/// Schedules synchronous and asynchronous work on a reusable pool of single-threaded apartment threads.
/// </summary>
public sealed class STAThreadPool {
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
            if (Workers.Count <= workerCountMin) {
                return;
            }
            var removable = Workers.Count - workerCountMin;
            var idle = default(List<STAThreadWorker>);
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
        var worker = default(STAThreadWorker);
        lock (Workers) {
            var idle = Workers.FirstOrDefault(w => w.Working == false);
            if (idle == null && WorkerCountMax > Workers.Count) {
                idle = new STAThreadWorker(Name, ++WorkerID);
                Workers.Add(idle);
            }
            if (idle == null) {
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

    private async Task<TResult> DoWork<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
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

    private async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
        return await DoWork(item, cancellationToken);
    }

    /// <summary>
    /// Gets or sets the length of time an inactive worker may be retained before it is removed.
    /// </summary>
    public TimeSpan IdleTime { get; set; } = TimeSpan.FromMinutes(2.5);

    /// <summary>
    /// Gets or sets the delay, in milliseconds, before retrying when all workers are busy. The value must be 1 or greater.
    /// </summary>
    public int TryWorkDelay {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(TryWorkDelay),
                    actualValue: value,
                    message: $"The value of '{nameof(TryWorkDelay)}' must be 1 or greater.");
            }
            field = value;
        }
    } = 10;

    /// <summary>
    /// Gets or sets the maximum number of workers that can be created. The value must be 1 or greater and cannot be less than <see cref="WorkerCountMin"/>.
    /// </summary>
    public int WorkerCountMax {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMax),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMax)}' must be 1 or greater.");
            }
            if (value < WorkerCountMin) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMax),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMax)}' cannot be less than " +
                             $"the value of '{nameof(WorkerCountMin)}'.");
            }
            field = value;
        }
    } = 16;

    /// <summary>
    /// Gets or sets the minimum number of idle workers retained by the pool. The value must be 1 or greater and cannot exceed <see cref="WorkerCountMax"/>.
    /// </summary>
    public int WorkerCountMin {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMin),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMin)}' must be 1 or greater.");
            }
            if (value > WorkerCountMax) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMin),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMin)}' cannot be greater than " +
                             $"the value of '{nameof(WorkerCountMax)}'.");
            }
            field = value;
        }
    } = 1;

    /// <summary>
    /// Gets the name used to identify this pool and its worker threads.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="STAThreadPool"/> class.
    /// </summary>
    /// <param name="name">The name used to identify the pool and its worker threads.</param>
    public STAThreadPool(string name) {
        Name = name;
    }

    /// <summary>
    /// Runs synchronous work on an available STA worker.
    /// </summary>
    /// <typeparam name="TResult">The type returned by <paramref name="work"/>.</typeparam>
    /// <param name="name">The name used to identify the work item.</param>
    /// <param name="work">The synchronous work to run.</param>
    /// <param name="cancellationToken">The token used to cancel the work.</param>
    /// <returns>A task that completes with the result of <paramref name="work"/>.</returns>
    public Task<TResult> Work<TResult>(string name, Func<TResult> work, CancellationToken cancellationToken) {
        return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
    }

    /// <summary>
    /// Runs asynchronous work on an available STA worker.
    /// </summary>
    /// <typeparam name="TResult">The type returned by <paramref name="work"/>.</typeparam>
    /// <param name="name">The name used to identify the work item.</param>
    /// <param name="work">The asynchronous work to run.</param>
    /// <param name="cancellationToken">The token used to cancel the work.</param>
    /// <returns>A task that completes with the result of <paramref name="work"/>.</returns>
    public Task<TResult> Work<TResult>(string name,
                                       Func<CancellationToken, Task<TResult>> work,
                                       CancellationToken cancellationToken) {
        return Work(new STAThreadWorkItem<TResult>(name, work), cancellationToken);
    }

    /// <summary>
    /// Runs synchronous work on an available STA worker.
    /// </summary>
    /// <param name="name">The name used to identify the work item.</param>
    /// <param name="work">The synchronous work to run.</param>
    /// <param name="cancellationToken">The token used to cancel the work.</param>
    /// <returns>A task that completes when <paramref name="work"/> has finished.</returns>
    public async Task Work(string name, Action work, CancellationToken cancellationToken) {
        await Work<object>(name, () => {
            if (work is not null) {
                work();
            }
            return default;
        }, cancellationToken);
    }

    /// <summary>
    /// Runs asynchronous work on an available STA worker.
    /// </summary>
    /// <param name="name">The name used to identify the work item.</param>
    /// <param name="work">The asynchronous work to run.</param>
    /// <param name="cancellationToken">The token used to cancel the work.</param>
    /// <returns>A task that completes when <paramref name="work"/> has finished.</returns>
    public async Task Work(string name, Func<CancellationToken, Task> work, CancellationToken cancellationToken) {
        await Work<object>(name, async token => {
            if (work is not null) {
                await work(token);
            }
            return default;
        }, cancellationToken);
    }

    /// <summary>
    /// Removes all workers from the pool and requests that their STA message loops exit. A worker
    /// that is running work when this is called exits once that work has completed.
    /// </summary>
    public void Empty() {
        if (Log.Info()) {
            Log.Info(nameof(Empty));
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
