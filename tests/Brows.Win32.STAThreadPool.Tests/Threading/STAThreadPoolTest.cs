using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading;

[TestFixture]
internal sealed class STAThreadPoolTest {
    private static async Task Occupy(STAThreadPool pool, int count) {
        var threads = new ConcurrentBag<Thread>();
        using var release = new ManualResetEventSlim();
        var work = Enumerable
            .Range(0, count)
            .Select(i => ((ISTAThreadPool)pool).Work("occupy-" + i, () => {
                threads.Add(Thread.CurrentThread);
                return release.Wait(TimeSpan.FromSeconds(10));
            }, CancellationToken.None))
            .ToArray();
        while (threads.Count < count) {
            await Task.Delay(10);
        }
        Assert.That(pool.WorkerCount, Is.EqualTo(count));
        release.Set();
        await Task.WhenAll(work);
    }

    [Test]
    public void Constructor_copies_options_into_a_distinct_instance() {
        var options = new STAThreadPoolOptions {
            IdleTime = TimeSpan.FromMinutes(5),
            TryWorkDelay = 25,
            WorkerCountMax = 4,
            WorkerCountMin = 2,
        };

        var pool = new STAThreadPool("options-copy", options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(pool.Options, Is.EqualTo(options));
            Assert.That(pool.Options, Is.Not.SameAs(options));
        }
    }

    [Test]
    public void IdleTime_getter_reads_the_current_options_value() {
        var pool = new STAThreadPool("options");
        pool.Options.IdleTime = TimeSpan.FromMinutes(5);

        Assert.That(pool.IdleTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void IdleTime_setter_updates_the_options_value() {
        var pool = new STAThreadPool("options");

        pool.IdleTime = TimeSpan.FromMinutes(5);

        Assert.That(pool.Options.IdleTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
    }

    [Test]
    public void TryWorkDelay_getter_reads_the_current_options_value() {
        var pool = new STAThreadPool("options");
        pool.Options.TryWorkDelay = 25;

        Assert.That(pool.TryWorkDelay, Is.EqualTo(25));
    }

    [Test]
    public void TryWorkDelay_setter_updates_the_options_value() {
        var pool = new STAThreadPool("options");

        pool.TryWorkDelay = 25;

        Assert.That(pool.Options.TryWorkDelay, Is.EqualTo(25));
    }

    [Test]
    public void WorkerCountMax_getter_reads_the_current_options_value() {
        var pool = new STAThreadPool("options");
        pool.Options.WorkerCountMax = 4;

        Assert.That(pool.WorkerCountMax, Is.EqualTo(4));
    }

    [Test]
    public void WorkerCountMax_setter_updates_the_options_value() {
        var pool = new STAThreadPool("options");

        pool.WorkerCountMax = 4;

        Assert.That(pool.Options.WorkerCountMax, Is.EqualTo(4));
    }

    [TestCase(1)]
    [TestCase(100)]
    public void TryWorkDelay_accepts_positive_values(int value) {
        var pool = new STAThreadPool("validation") {
            TryWorkDelay = value,
        };

        Assert.That(pool.TryWorkDelay, Is.EqualTo(value));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void TryWorkDelay_rejects_non_positive_values_without_changing_the_existing_value(int value) {
        var pool = new STAThreadPool("validation");
        const int existingValue = 10;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.TryWorkDelay = value);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPool.TryWorkDelay)));
            Assert.That(pool.TryWorkDelay, Is.EqualTo(existingValue));
        }
    }

    [TestCase(1)]
    [TestCase(100)]
    public void WorkerCountMax_accepts_positive_values(int value) {
        var pool = new STAThreadPool("validation") {
            WorkerCountMax = value,
        };

        Assert.That(pool.WorkerCountMax, Is.EqualTo(value));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void WorkerCountMax_rejects_non_positive_values_without_changing_the_existing_value(int value) {
        var pool = new STAThreadPool("validation");
        var existingValue = Environment.ProcessorCount;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.WorkerCountMax = value);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPool.WorkerCountMax)));
            Assert.That(pool.WorkerCountMax, Is.EqualTo(existingValue));
        }
    }

    [TestCase(1)]
    [TestCase(100)]
    public void WorkerCountMin_accepts_positive_values(int value) {
        var pool = new STAThreadPool("validation") {
            WorkerCountMax = value,
            WorkerCountMin = value,
        };

        Assert.That(pool.WorkerCountMin, Is.EqualTo(value));
    }

    [Test]
    public void WorkerCountMin_accepts_zero() {
        var pool = new STAThreadPool("validation") {
            WorkerCountMin = 0,
        };

        Assert.That(pool.WorkerCountMin, Is.EqualTo(0));
    }

    [TestCase(-1)]
    [TestCase(-100)]
    public void WorkerCountMin_rejects_negative_values_without_changing_the_existing_value(int value) {
        var pool = new STAThreadPool("validation");
        const int existingValue = 1;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.WorkerCountMin = value);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPool.WorkerCountMin)));
            Assert.That(pool.WorkerCountMin, Is.EqualTo(existingValue));
        }
    }

    [Test]
    public async Task Empty_exits_a_worker_thread_whose_context_is_still_starting() {
        ISTAThreadPool pool = new STAThreadPool("empty-starting");
        var thread = default(Thread);

        var work = pool.Work("starting", () => thread = Thread.CurrentThread, CancellationToken.None);
        pool.Empty();

        await work;
        Assert.That(() => thread.IsAlive, Is.False.After(10000, 50));
    }

    [Test]
    public async Task Empty_completes_work_that_is_already_running() {
        ISTAThreadPool pool = new STAThreadPool("empty-running");
        using var workStarted = new ManualResetEventSlim();
        using var releaseWork = new ManualResetEventSlim();
        var thread = default(Thread);

        var work = pool.Work("running", () => {
            thread = Thread.CurrentThread;
            workStarted.Set();
            return releaseWork.Wait(TimeSpan.FromSeconds(10));
        }, CancellationToken.None);
        Assert.That(workStarted.Wait(TimeSpan.FromSeconds(10)), Is.True);
        pool.Empty();
        releaseWork.Set();

        Assert.That(await work, Is.True);
        Assert.That(() => thread.IsAlive, Is.False.After(10000, 50));
    }

    [Test]
    public async Task Idle_workers_are_not_removed_below_the_minimum_worker_count() {
        var pool = new STAThreadPool("reap-minimum") {
            WorkerCountMax = 4,
            WorkerCountMin = 2,
            IdleTime = TimeSpan.Zero,
            TimerPeriod = TimeSpan.FromMilliseconds(50),
        };
        try {
            await Occupy(pool, 4);

            Assert.That(() => pool.WorkerCount, Is.EqualTo(2).After(10000, 50));
            await Task.Delay(500);
            Assert.That(pool.WorkerCount, Is.EqualTo(2));
        }
        finally {
            ((ISTAThreadPool)pool).Empty();
        }
    }

    [Test]
    public async Task Idle_workers_are_removed_while_the_pool_keeps_working() {
        var pool = new STAThreadPool("reap-busy") {
            WorkerCountMax = 4,
            WorkerCountMin = 1,
            IdleTime = TimeSpan.Zero,
            TimerPeriod = TimeSpan.FromMilliseconds(50),
        };
        using var stop = new CancellationTokenSource();
        try {
            await Occupy(pool, 4);
            var keepWorking = Task.Run(async () => {
                while (stop.IsCancellationRequested == false) {
                    await ((ISTAThreadPool)pool).Work("tick", () => 0, CancellationToken.None);
                    await Task.Delay(10, CancellationToken.None);
                }
            });

            Assert.That(() => pool.WorkerCount, Is.EqualTo(1).After(10000, 50));

            stop.Cancel();
            await keepWorking;
        }
        finally {
            ((ISTAThreadPool)pool).Empty();
        }
    }

    [Test]
    public void WorkerCountMax_rejects_a_value_less_than_the_current_minimum_without_changing_either_value() {
        var pool = new STAThreadPool("validation") {
            WorkerCountMax = 4,
            WorkerCountMin = 2,
        };
        const int invalidValue = 1;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.WorkerCountMax = invalidValue);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPool.WorkerCountMax)));
            Assert.That(pool.WorkerCountMax, Is.EqualTo(4));
            Assert.That(pool.WorkerCountMin, Is.EqualTo(2));
        }
    }

    [Test]
    public void WorkerCountMin_rejects_a_value_greater_than_the_current_maximum_without_changing_either_value() {
        var pool = new STAThreadPool("validation") {
            WorkerCountMax = 2,
        };
        const int invalidValue = 3;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => pool.WorkerCountMin = invalidValue);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPool.WorkerCountMin)));
            Assert.That(pool.WorkerCountMax, Is.EqualTo(2));
            Assert.That(pool.WorkerCountMin, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Work_runs_synchronous_work_on_an_sta_worker_and_returns_its_result() {
        ISTAThreadPool pool = new STAThreadPool("synchronous");
        try {
            var result = await pool.Work("get-thread", () => new {
                ApartmentState = Thread.CurrentThread.GetApartmentState(),
                ThreadName = Thread.CurrentThread.Name,
            }, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(result.ApartmentState, Is.EqualTo(ApartmentState.STA));
                Assert.That(result.ThreadName, Does.StartWith("STAThreadWorker synchronous."));
                Assert.That(pool.Name, Is.EqualTo("synchronous"));
            }
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_runs_asynchronous_continuations_on_the_same_sta_worker() {
        ISTAThreadPool pool = new STAThreadPool("asynchronous");
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

            using (Assert.EnterMultipleScope()) {
                Assert.That(result.BeforeAwait, Is.EqualTo(result.AfterAwait));
                Assert.That(result.ApartmentState, Is.EqualTo(ApartmentState.STA));
                Assert.That(result.ReceivedToken, Is.EqualTo(cancellation.Token));
            }
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_reuses_an_idle_worker() {
        ISTAThreadPool pool = new STAThreadPool("reuse");
        try {
            var firstWorker = await pool.Work("first", () =>
                Thread.CurrentThread.ManagedThreadId, CancellationToken.None);
            var secondWorker = await pool.Work("second", () =>
                Thread.CurrentThread.ManagedThreadId, CancellationToken.None);

            Assert.That(secondWorker, Is.EqualTo(firstWorker));
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_waits_for_capacity_when_all_workers_are_busy() {
        ISTAThreadPool pool = new STAThreadPool("capacity") {
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
        ISTAThreadPool pool = new STAThreadPool("cancellation");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invoked = false;

        try {
            var work = pool.Work("cancelled", () => {
                invoked = true;
                return 1;
            }, cancellation.Token);

            Assert.That(async () => await work, Throws.InstanceOf<OperationCanceledException>());
            using (Assert.EnterMultipleScope()) {
                Assert.That(work.IsCanceled, Is.True);
                Assert.That(invoked, Is.False);
            }
        }
        finally {
            pool.Empty();
        }
    }

    [Test]
    public async Task Work_does_not_create_a_worker_when_the_token_is_already_cancelled() {
        var pool = new STAThreadPool("cancelled-worker");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try {
            var work = ((ISTAThreadPool)pool).Work("cancelled", () => 1, cancellation.Token);

            Assert.That(async () => await work, Throws.InstanceOf<OperationCanceledException>());
            Assert.That(pool.WorkerCount, Is.Zero);
        }
        finally {
            ((ISTAThreadPool)pool).Empty();
        }
    }

    [Test]
    public async Task Work_propagates_exceptions_from_synchronous_and_asynchronous_callbacks() {
        ISTAThreadPool pool = new STAThreadPool("exceptions");
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
        ISTAThreadPool pool = new STAThreadPool("non-generic");
        var synchronousCompleted = false;
        var asynchronousCompleted = false;

        try {
            await pool.Work("sync", () => {
                synchronousCompleted = true;
            }, CancellationToken.None);
            await pool.Work("async", async _ => {
                await Task.Yield();
                asynchronousCompleted = true;
            }, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(synchronousCompleted, Is.True);
                Assert.That(asynchronousCompleted, Is.True);
            }
        }
        finally {
            pool.Empty();
        }
    }

    [TestCase(nameof(ISTAThreadPool.Work))]
    [TestCase(nameof(ISTAThreadPool.Empty))]
    public void Pool_operations_are_not_exposed_as_public_class_methods(string name) {
        Assert.That(typeof(STAThreadPool).GetMethods().Select(method => method.Name), Does.Not.Contain(name));
    }
}
