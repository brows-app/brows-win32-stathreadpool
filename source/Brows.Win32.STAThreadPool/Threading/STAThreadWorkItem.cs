using System;
using System.Threading.Tasks;

namespace Brows.Threading;

internal sealed class STAThreadWorkItem<TResult> {
    public string Name { get; }
    public bool Async { get; }
    public Func<TResult> Function { get; }
    public Func<CancellationToken, Task<TResult>> FunctionAsync { get; }

    public STAThreadWorkItem(string name, Func<TResult> function) {
        Name = name;
        Async = false;
        Function = function;
    }

    public STAThreadWorkItem(string name, Func<CancellationToken, Task<TResult>> functionAsync) {
        Name = name;
        Async = true;
        FunctionAsync = functionAsync;
    }

    public async Task<TResult> Invoke(CancellationToken cancellationToken) {
        var result = default(TResult);
        if (Async) {
            var function = FunctionAsync;
            var functionTask = function?.Invoke(cancellationToken);
            if (functionTask is not null) {
                result = await functionTask;
            }
        }
        else {
            var function = Function;
            if (function is not null) {
                result = function();
            }
        }
        return result;
    }
}
