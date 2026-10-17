using Brows.Composition;

namespace Brows.Threading;

[TestFixture]
internal sealed class ISTAThreadPoolProviderTest {
    [Test]
    public void Interface_extends_the_composition_export_interface() {
        Assert.That(typeof(IExport).IsAssignableFrom(typeof(ISTAThreadPoolProvider)), Is.True);
    }

    [Test]
    public void Interface_declares_a_get_thread_pool_method_that_accepts_a_target_and_returns_an_sta_thread_pool() {
        var method = typeof(ISTAThreadPoolProvider).GetMethod(nameof(ISTAThreadPoolProvider.GetThreadPool));

        Assert.That(method, Is.Not.Null);
        var parameters = method.GetParameters();

        using (Assert.EnterMultipleScope()) {
            Assert.That(method.IsPublic, Is.True);
            Assert.That(method.IsAbstract, Is.True);
            Assert.That(method.IsStatic, Is.False);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(ISTAThreadPool)));
            Assert.That(parameters, Has.Length.EqualTo(1));
        }

        using (Assert.EnterMultipleScope()) {
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(object)));
            Assert.That(parameters[0].IsOptional, Is.False);
        }
    }
}
