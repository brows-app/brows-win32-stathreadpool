
namespace Brows.Threading;

internal sealed class STAThreadPoolFactory : ISTAThreadPoolFactory {
    ISTAThreadPool ISTAThreadPoolFactory.CreateThreadPool(string name, STAThreadPoolOptions options) {
        return new STAThreadPool(name, options);
    }
}
