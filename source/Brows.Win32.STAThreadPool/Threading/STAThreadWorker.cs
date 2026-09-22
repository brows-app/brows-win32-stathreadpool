using Domore.Logs;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Brows.Threading;

internal sealed class STAThreadWorker {
    private static readonly ILog Log = Logging.For(typeof(STAThreadWorker));

    private bool ExitRequested;
    private Stopwatch Stopwatch;

    private STAThreadContext Context => field ??=
        new($"{nameof(STAThreadWorker)} {Pool}.{ID:00}");

    public TimeSpan? IdleTime =>
        Stopwatch?.Elapsed;

    public bool Working { get; set; }
    public long ID { get; }
    public string Pool { get; }

    public STAThreadWorker(string pool, long id) {
        ID = id;
        Pool = pool;
    }

    // Exit, ExitPending and Working are only ever touched by the pool while it holds
    // its worker lock, so a worker that is working defers its exit until the work is
    // done. Exiting the message loop while work is pending would strand that work.
    public void Exit() {
        if (Log.Info()) {
            Log.Info(this + " " + nameof(Exit) + " [" + IdleTime + "]");
        }
        ExitRequested = true;
        if (Working == false) {
            Context.Exit();
        }
    }

    public void ExitPending() {
        if (ExitRequested) {
            Context.Exit();
        }
    }

    public async Task<TResult> Work<TResult>(STAThreadWorkItem<TResult> item, CancellationToken cancellationToken) {
        if (Log.Info()) {
            Log.Info(this + " [" + item?.Name + "]");
        }
        Stopwatch = null;
        try {
            if (cancellationToken.IsCancellationRequested) {
                return await Task.FromCanceled<TResult>(cancellationToken);
            }
            var taskSource = new TaskCompletionSource<TResult>();
            var
            context = await Context.Ready();
            context.Post(state: null, d: async _ => {
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    taskSource.SetResult(item == null
                        ? default
                        : await item.Invoke(cancellationToken));
                }
                catch (Exception ex) {
                    if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested) {
                        taskSource.TrySetCanceled(cancellationToken);
                    }
                    else {
                        taskSource.TrySetException(ex);
                    }
                }
            });
            return await taskSource.Task;
        }
        finally {
            Stopwatch = Stopwatch.StartNew();
        }
    }

    public sealed override string ToString() {
        return Pool + "." + ID.ToString("00") + (Working ? " [work]" : " [idle]");
    }
}
