using System;
using System.Linq;
using System.Reflection;

namespace Brows.Threading;

[TestFixture]
internal sealed class STAThreadPoolOptionsTest {
    private static void ClearBackingField(STAThreadPoolOptions options, string name) {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var field = typeof(STAThreadPoolOptions)
            .GetFields(flags)
            .SingleOrDefault(candidate => candidate.Name.Contains(name));

        Assert.That(field, Is.Not.Null, $"No backing field was found for '{name}'.");

        field.SetValue(options, 0);
    }

    [Test]
    public void Properties_have_the_documented_default_values() {
        var options = new STAThreadPoolOptions();

        using (Assert.EnterMultipleScope()) {
            Assert.That(options.IdleTime, Is.EqualTo(TimeSpan.FromMinutes(2.5)));
            Assert.That(options.TryWorkDelay, Is.EqualTo(10));
            Assert.That(options.WorkerCountMax, Is.EqualTo(Environment.ProcessorCount));
            Assert.That(options.WorkerCountMin, Is.EqualTo(1));
        }
    }

    [Test]
    public void IdleTime_accepts_and_returns_an_assigned_value() {
        var options = new STAThreadPoolOptions();
        var idleTime = TimeSpan.FromMinutes(5);

        options.IdleTime = idleTime;

        Assert.That(options.IdleTime, Is.EqualTo(idleTime));
    }

    [TestCase(1)]
    [TestCase(100)]
    public void TryWorkDelay_accepts_positive_values(int value) {
        var options = new STAThreadPoolOptions {
            TryWorkDelay = value,
        };

        Assert.That(options.TryWorkDelay, Is.EqualTo(value));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void TryWorkDelay_rejects_non_positive_values_without_changing_the_existing_value(int value) {
        var options = new STAThreadPoolOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.TryWorkDelay = value);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPoolOptions.TryWorkDelay)));
            Assert.That(options.TryWorkDelay, Is.EqualTo(10));
        }
    }

    [TestCase(1)]
    [TestCase(100)]
    public void WorkerCountMax_accepts_positive_values_not_less_than_the_minimum(int value) {
        var options = new STAThreadPoolOptions {
            WorkerCountMax = value,
        };

        Assert.That(options.WorkerCountMax, Is.EqualTo(value));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void WorkerCountMax_rejects_non_positive_values_without_changing_the_existing_value(int value) {
        var options = new STAThreadPoolOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.WorkerCountMax = value);

        using (Assert.EnterMultipleScope()) {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPoolOptions.WorkerCountMax)));
            Assert.That(options.WorkerCountMax, Is.EqualTo(Environment.ProcessorCount));
        }
    }

    [Test]
    public void WorkerCountMax_rejects_a_value_less_than_the_minimum_without_changing_either_value() {
        var options = new STAThreadPoolOptions {
            WorkerCountMax = 3,
            WorkerCountMin = 2,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.WorkerCountMax = 1);

        Assert.Multiple(() => {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPoolOptions.WorkerCountMax)));
            Assert.That(options.WorkerCountMax, Is.EqualTo(3));
            Assert.That(options.WorkerCountMin, Is.EqualTo(2));
        });
    }

    [TestCase(1)]
    [TestCase(100)]
    public void WorkerCountMin_accepts_non_negative_values_not_greater_than_the_maximum(int value) {
        var options = new STAThreadPoolOptions {
            WorkerCountMax = value,
            WorkerCountMin = value,
        };

        Assert.That(options.WorkerCountMin, Is.EqualTo(value));
    }

    [Test]
    public void WorkerCountMin_accepts_zero() {
        var options = new STAThreadPoolOptions {
            WorkerCountMin = 0,
        };

        Assert.That(options.WorkerCountMin, Is.Zero);
    }

    [TestCase(-1)]
    [TestCase(-100)]
    public void WorkerCountMin_rejects_negative_values_without_changing_the_existing_value(int value) {
        var options = new STAThreadPoolOptions();

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.WorkerCountMin = value);

        Assert.Multiple(() => {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPoolOptions.WorkerCountMin)));
            Assert.That(options.WorkerCountMin, Is.EqualTo(1));
        });
    }

    [Test]
    public void WorkerCountMin_rejects_a_value_greater_than_the_maximum_without_changing_either_value() {
        var options = new STAThreadPoolOptions {
            WorkerCountMax = 2,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => options.WorkerCountMin = 3);

        Assert.Multiple(() => {
            Assert.That(exception.ParamName, Is.EqualTo(nameof(STAThreadPoolOptions.WorkerCountMin)));
            Assert.That(options.WorkerCountMax, Is.EqualTo(2));
            Assert.That(options.WorkerCountMin, Is.EqualTo(1));
        });
    }

    [Test]
    public void WorkerCountMin_is_assigned_before_WorkerCountMax_is_assigned() {
        /*
         * Property initializers on a new instance run in declaration order, so the
         * WorkerCountMin setter must also work when the WorkerCountMax backing field
         * has not been assigned yet. Reset the backing field to that state.
         */
        var options = new STAThreadPoolOptions();
        ClearBackingField(options, nameof(STAThreadPoolOptions.WorkerCountMax));

        options.WorkerCountMin = 1;
        options.WorkerCountMax = Environment.ProcessorCount;

        using (Assert.EnterMultipleScope()) {
            Assert.That(options.WorkerCountMin, Is.EqualTo(1));
            Assert.That(options.WorkerCountMax, Is.EqualTo(Environment.ProcessorCount));
        }
    }
}
