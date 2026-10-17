using Brows.Composition;

namespace Brows.Threading;

[TestFixture]
internal sealed class ISTAThreadPoolFactoryTest {
    [Test]
    public void Interface_extends_the_composition_export_interface() {
        Assert.That(typeof(IExport).IsAssignableFrom(typeof(ISTAThreadPoolFactory)), Is.True);
    }

    [Test]
    public void Interface_declares_a_create_thread_pool_method_that_returns_an_sta_thread_pool() {
        var createThreadPool = typeof(ISTAThreadPoolFactory).GetMethod(nameof(ISTAThreadPoolFactory.CreateThreadPool));

        using (Assert.EnterMultipleScope()) {
            Assert.That(createThreadPool, Is.Not.Null);
            Assert.That(createThreadPool.ReturnType, Is.EqualTo(typeof(ISTAThreadPool)));
            Assert.That(createThreadPool.GetParameters(), Has.Length.EqualTo(2));
            Assert.That(createThreadPool.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string)));
            Assert.That(createThreadPool.GetParameters()[1].ParameterType, Is.EqualTo(typeof(STAThreadPoolOptions)));
        }
    }

    [Test]
    public void CreateThreadPool_options_parameter_is_optional_with_a_null_default() {
        var method = typeof(ISTAThreadPoolFactory).GetMethod(nameof(ISTAThreadPoolFactory.CreateThreadPool));
        var options = method.GetParameters()[1];

        using (Assert.EnterMultipleScope()) {
            Assert.That(options.IsOptional, Is.True);
            Assert.That(options.HasDefaultValue, Is.True);
            Assert.That(options.DefaultValue, Is.Null);
        }
    }
}
