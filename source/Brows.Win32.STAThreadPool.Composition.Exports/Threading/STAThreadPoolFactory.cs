
namespace Brows.Threading;

internal sealed class STAThreadPoolFactory : ISTAThreadPoolFactory {
    ISTAThreadPool ISTAThreadPoolFactory.Create(string name, STAThreadPoolOptions options) {
        return new STAThreadPool(name, options);
    }
}
