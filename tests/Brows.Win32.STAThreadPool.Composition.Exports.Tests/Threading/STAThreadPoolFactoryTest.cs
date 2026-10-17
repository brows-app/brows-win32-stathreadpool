using Brows.Composition;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Threading;

[TestFixture]
internal sealed class STAThreadPoolFactoryTest {
    [Test]
    public void Factory_type_is_internal_and_exported_to_the_composition_framework() {
        var type = typeof(STAThreadPoolFactory);

        using (Assert.EnterMultipleScope()) {
            Assert.That(type.IsVisible, Is.False);
            Assert.That(type.GetConstructor(Type.EmptyTypes), Is.Not.Null);
            Assert.That(typeof(IExport).IsAssignableFrom(type), Is.True);
            Assert.That(typeof(ISTAThreadPoolFactory).IsAssignableFrom(type), Is.True);
        }
    }

    [Test]
    public void Create_returns_a_pool_with_the_given_name_and_option_values() {
        ISTAThreadPoolFactory factory = new STAThreadPoolFactory();
        var options = new STAThreadPoolOptions {
            IdleTime = TimeSpan.FromMinutes(5),
            TryWorkDelay = 25,
            WorkerCountMax = 4,
            WorkerCountMin = 2,
        };

        var pool = factory.Create("factory-options", options);

        using (Assert.EnterMultipleScope()) {
            Assert.That(pool, Is.InstanceOf<STAThreadPool>());
            Assert.That(pool.Name, Is.EqualTo("factory-options"));
            Assert.That(pool.IdleTime, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(pool.TryWorkDelay, Is.EqualTo(25));
            Assert.That(pool.WorkerCountMax, Is.EqualTo(4));
            Assert.That(pool.WorkerCountMin, Is.EqualTo(2));
        }
    }

    [Test]
    public void Create_applies_defaults_when_the_options_are_null() {
        ISTAThreadPoolFactory factory = new STAThreadPoolFactory();

        var pool = factory.Create("factory-defaults", null);

        using (Assert.EnterMultipleScope()) {
            Assert.That(pool.Name, Is.EqualTo("factory-defaults"));
            Assert.That(pool.IdleTime, Is.EqualTo(TimeSpan.FromMinutes(2.5)));
            Assert.That(pool.TryWorkDelay, Is.EqualTo(10));
            Assert.That(pool.WorkerCountMax, Is.EqualTo(Environment.ProcessorCount));
            Assert.That(pool.WorkerCountMin, Is.EqualTo(1));
        }
    }

    [Test]
    public void Create_returns_a_distinct_pool_for_each_call_while_reusing_the_factory() {
        ISTAThreadPoolFactory factory = new STAThreadPoolFactory();

        var first = factory.Create("factory-distinct", null);
        var second = factory.Create("factory-distinct", null);

        Assert.That(second, Is.Not.SameAs(first));

        ((ISTAThreadPool)first).Empty();
        ((ISTAThreadPool)second).Empty();
    }

    [Test]
    public async Task Create_returns_a_pool_that_runs_work_on_an_sta_worker() {
        ISTAThreadPoolFactory factory = new STAThreadPoolFactory();
        var pool = factory.Create("factory-sta", null);
        try {
            var result = await pool.Work("check", () => new {
                ApartmentState = Thread.CurrentThread.GetApartmentState(),
                ThreadName = Thread.CurrentThread.Name,
            }, CancellationToken.None);

            using (Assert.EnterMultipleScope()) {
                Assert.That(result.ApartmentState, Is.EqualTo(ApartmentState.STA));
                Assert.That(result.ThreadName, Does.StartWith("STAThreadWorker factory-sta."));
            }
        }
        finally {
            pool.Empty();
        }
    }
}
