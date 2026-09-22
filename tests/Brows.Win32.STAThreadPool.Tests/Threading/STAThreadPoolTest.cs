using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading;

[TestFixture]
internal sealed class STAThreadPoolTest {
    [Test]
    public async Task Work_runs_synchronous_work_on_an_sta_worker_and_returns_its_result() {
        var pool = new STAThreadPool("synchronous");
        try {
            var result = await pool.Work("get-thread", () => new {
                ApartmentState = Thread.CurrentThread.GetApartmentState(),
                ThreadName = Thread.CurrentThread.Name,
            }, CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(result.ApartmentState, Is.EqualTo(ApartmentState.STA));
                Assert.That(result.ThreadName, Does.StartWith("STAThreadWorker synchronous."));
                Assert.That(pool.Name, Is.EqualTo("synchronous"));
            });
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_runs_asynchronous_continuations_on_the_same_sta_worker() {
        var pool = new STAThreadPool("asynchronous");
        using var cancellation = new CancellationTokenSource();
        try {
            var result = await pool.Work("continue", async token => {
                var beforeAwait = Thread.CurrentThread.ManagedThreadId;
                await Task.Yield();
                return new {
                    BeforeAwait = beforeAwait,
                    AfterAwait = Thread.CurrentThread.ManagedThreadId,
                    ApartmentState = Thread.CurrentThread.GetApartmentState(),
                    ReceivedToken = token,
                };
            }, cancellation.Token);

            Assert.Multiple(() => {
                Assert.That(result.BeforeAwait, Is.EqualTo(result.AfterAwait));
                Assert.That(result.ApartmentState, Is.EqualTo(ApartmentState.STA));
                Assert.That(result.ReceivedToken, Is.EqualTo(cancellation.Token));
            });
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_reuses_an_idle_worker() {
        var pool = new STAThreadPool("reuse");
        try {
            var firstWorker = await pool.Work("first", () => Thread.CurrentThread.ManagedThreadId, CancellationToken.None);
            var secondWorker = await pool.Work("second", () => Thread.CurrentThread.ManagedThreadId, CancellationToken.None);

            Assert.That(secondWorker, Is.EqualTo(firstWorker));
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_waits_for_capacity_when_all_workers_are_busy() {
        var pool = new STAThreadPool("capacity") {
            TryWorkDelay = 1,
            WorkerCountMax = 1,
        };
        using var firstWorkStarted = new ManualResetEventSlim();
        using var releaseFirstWork = new ManualResetEventSlim();

        try {
            var first = pool.Work("first", () => {
                firstWorkStarted.Set();
                Assert.That(releaseFirstWork.Wait(TimeSpan.FromSeconds(5)), Is.True);
                return Thread.CurrentThread.ManagedThreadId;
            }, CancellationToken.None);
            Assert.That(firstWorkStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);

            var second = pool.Work("second", () => Thread.CurrentThread.ManagedThreadId, CancellationToken.None);
            Assert.That(second.IsCompleted, Is.False);

            releaseFirstWork.Set();
            var threadIds = await Task.WhenAll(first, second);

            Assert.That(threadIds[1], Is.EqualTo(threadIds[0]));
        }
        finally {
            releaseFirstWork.Set();
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_honors_cancellation_before_work_is_dispatched() {
        var pool = new STAThreadPool("cancellation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        try {
            var work = pool.Work("cancelled", () => {
                invoked = true;
                return 1;
            }, cancellation.Token);

            Assert.That(async () => await work, Throws.InstanceOf<OperationCanceledException>());
            Assert.Multiple(() => {
                Assert.That(work.IsCanceled, Is.True);
                Assert.That(invoked, Is.False);
            });
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_propagates_exceptions_from_synchronous_and_asynchronous_callbacks() {
        var pool = new STAThreadPool("exceptions");
        try {
            var synchronous = pool.Work<int>("sync-fault", () => throw new InvalidOperationException("sync"), CancellationToken.None);
            var asynchronous = pool.Work<int>("async-fault", async _ => {
                await Task.Yield();
                throw new InvalidOperationException("async");
            }, CancellationToken.None);

            Assert.That(async () => await synchronous, Throws.TypeOf<InvalidOperationException>());
            Assert.That(async () => await asynchronous, Throws.TypeOf<InvalidOperationException>());
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Non_generic_work_overloads_complete_their_callbacks() {
        var pool = new STAThreadPool("non-generic");
        var synchronousCompleted = false;
        var asynchronousCompleted = false;

        try {
            await pool.Work("sync", () => synchronousCompleted = true, CancellationToken.None);
            await pool.Work("async", async _ => {
                await Task.Yield();
                asynchronousCompleted = true;
            }, CancellationToken.None);

            Assert.Multiple(() => {
                Assert.That(synchronousCompleted, Is.True);
                Assert.That(asynchronousCompleted, Is.True);
            });
        }
        finally {
            pool.Empty();
        }
    }
}
