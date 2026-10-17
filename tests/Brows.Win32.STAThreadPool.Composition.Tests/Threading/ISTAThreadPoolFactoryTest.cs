using Brows.Composition;

namespace Brows.Threading;

[TestFixture]
internal sealed class ISTAThreadPoolFactoryTest {
    [Test]
    public void Interface_extends_the_composition_export_interface() {
        Assert.That(typeof(IExport).IsAssignableFrom(typeof(ISTAThreadPoolFactory)), Is.True);
    }

    [Test]
    public void Interface_declares_a_create_method_that_returns_an_sta_thread_pool() {
        var create = typeof(ISTAThreadPoolFactory).GetMethod(nameof(ISTAThreadPoolFactory.Create));

        using (Assert.EnterMultipleScope()) {
            Assert.That(create, Is.Not.Null);
            Assert.That(create.ReturnType, Is.EqualTo(typeof(ISTAThreadPool)));
            Assert.That(create.GetParameters(), Has.Length.EqualTo(2));
            Assert.That(create.GetParameters()[0].ParameterType, Is.EqualTo(typeof(string)));
            Assert.That(create.GetParameters()[1].ParameterType, Is.EqualTo(typeof(STAThreadPoolOptions)));
        }
    }
}
