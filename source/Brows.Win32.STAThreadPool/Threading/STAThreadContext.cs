using Domore.Logs;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Brows.Threading;

internal sealed class STAThreadContext {
    private static readonly ILog Log = Logging.For(typeof(STAThreadContext));

    private bool Exited;
    private SynchronizationContext Sync;
    private Task<SynchronizationContext> SyncTask;
    private readonly
#if NET9_0_OR_GREATER
        Lock
#else
        object
#endif
    Locker = new();

    private void ExitPost(SynchronizationContext sync) {
        if (sync is null) {
            return;
        }
        sync.Post(state: null, d: _ => {
            try {
                Application.ExitThread();
            }
            catch (Exception ex) {
                if (Log.Error()) {
                    Log.Error(Name, nameof(Application.ExitThread), ex);
                }
            }
        });
    }

    private async Task<SynchronizationContext> Start() {
        if (Log.Info()) {
            Log.Info(Name + " " + nameof(Start));
        }
        var sync = await Task.Run(() => {
            using (var threadIdle = new ManualResetEventSlim()) {
                var sync = default(SynchronizationContext);
                var thread = new Thread(() => {
                    try {
                        EventHandler idle = default;
                        Application.Idle += idle = (s, e) => {
                            if (Log.Info()) {
                                Log.Info(Name + " " + nameof(Application.Idle));
                            }
                            try {
                                sync = SynchronizationContext.Current;
                                Application.Idle -= idle;
                                threadIdle.Set();
                            }
                            catch (Exception ex) {
                                if (Log.Error()) {
                                    Log.Error(Name, nameof(Application.Idle), ex);
                                }
                            }
                        };
                        Application.Run();
                    }
                    catch (Exception ex) {
                        if (Log.Error()) {
                            Log.Error(Name, nameof(Application.Run), ex);
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Name = Name;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                threadIdle.Wait();
                return sync;
            }
        });
        var exited = false;
        lock (Locker) {
            Sync = sync;
            exited = Exited;
        }
        if (exited) {
            ExitPost(sync);
        }
        return sync;
    }

    public string Name { get; }

    public STAThreadContext(string name) {
        Name = name;
    }

    public async ValueTask<SynchronizationContext> Ready() {
        var syncTask = default(Task<SynchronizationContext>);
        lock (Locker) {
            syncTask = SyncTask ??= Start();
        }
        return await syncTask;
    }

    public void Exit() {
        if (Log.Info()) {
            Log.Info(Name + " " + nameof(Exit));
        }
        var sync = default(SynchronizationContext);
        lock (Locker) {
            if (Exited) {
                return;
            }
            Exited = true;
            sync = Sync;
        }
        ExitPost(sync);
    }

    public sealed override string ToString() {
        return Name;
    }
}
