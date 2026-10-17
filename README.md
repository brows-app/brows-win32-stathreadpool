# brows-win32-stathreadpool

Windows STA thread pool implementation for .NET, with a separate platform-neutral contracts package.

Some Windows APIs — shell namespace extensions, many COM servers, drag-and-drop, and the clipboard
among them — must be called from a thread in a single-threaded apartment that pumps messages. The
.NET thread pool is MTA, so `Brows.Win32.STAThreadPool` provides a small pool of reusable STA worker
threads and an `async`-friendly way to run work on them.

## Installation

Install the Windows implementation when you need to create and run a pool:

```
dotnet add package Brows.Win32.STAThreadPool
```

The implementation package depends on the contracts package, so its API types are available
transitively.

Install only the contracts when you need to reference or implement the shared API without the
Windows implementation:

```
dotnet add package Brows.Win32.STAThreadPool.Contracts
```

The contracts package contains `ISTAThreadPool` and `STAThreadPoolOptions`. It has no Windows-specific
runtime dependency. On .NET 8 and later, contract-only projects can target platform-neutral TFMs such
as `net8.0` or `net10.0` and run without Windows. The `.NET Framework` targets, `net462` and `net48`,
are Windows-only.

The implementation package targets `net462`, `net48`, `net8.0-windows`, and `net10.0-windows`. It
requires Windows because its STA workers use a Windows Forms message loop. On .NET 8 and later the
consuming project must target a Windows TFM, such as `net10.0-windows`; the Windows Desktop
framework reference the STA message loop needs flows in from this package.

Install the composition package when a pool is created through a composition import instead of a
direct reference to the implementation:

```
dotnet add package Brows.Win32.STAThreadPool.Composition
```

The composition package contains two contracts extending `Brows.Composition.IExport`:

- `ISTAThreadPoolFactory` creates a new `ISTAThreadPool` through `CreateThreadPool`.
- `ISTAThreadPoolProvider` resolves an `ISTAThreadPool` for a target object through `GetThreadPool`.

Like the contracts package, it is platform-neutral and has no Windows-specific runtime dependency.
The exports package provides the concrete factory the composition framework discovers:

```
dotnet add package Brows.Win32.STAThreadPool.Composition.Exports
```

The exports package contains the internal `STAThreadPoolFactory` implementation and returns the
Windows `STAThreadPool`, so creating pools this way has the same requirements as the implementation
package above.

## Usage

Create one pool and keep it for the lifetime of the component that needs STA access. Creating a pool
does not start any threads; workers are created on demand.

Use `ISTAThreadPool` to schedule work and empty the pool. `STAThreadPool` implements `Work` and
`Empty` explicitly, so these methods are not callable through a `STAThreadPool` reference without
casting it to `ISTAThreadPool`. The interface exposes configuration values as read-only properties;
the concrete pool exposes its configurable values with public setters and through its `Options`
property. Shared operation documentation is defined on `ISTAThreadPool` and inherited by
`STAThreadPool`.

```csharp
using Brows.Threading;

ISTAThreadPool pool = new STAThreadPool("shell");

// Synchronous work.
var name = await pool.Work("get-name", () => {
    return shellItem.GetDisplayName();
}, cancellationToken);

// Asynchronous work. Continuations after each await resume on the same STA thread.
var items = await pool.Work("enumerate", async token => {
    var folder = await OpenFolderAsync(token);
    return folder.Enumerate().ToList();
}, cancellationToken);

// Overloads without a result are also available.
await pool.Work("refresh", () => view.Refresh(), cancellationToken);
```

Every `Work` call is dispatched to an idle worker, or to a newly created one if the pool has not yet
reached `WorkerCountMax`. When all workers are busy, the call waits and retries until one frees up,
so a `Work` task may not begin immediately. The `CancellationToken` is honored before the work is
dispatched and is passed through to the asynchronous overloads; it does not interrupt a callback
that has already started.

Exceptions thrown by the callback are propagated to the awaiting caller, and cancellation surfaces
as an `OperationCanceledException`.

### Configuration

| Property | Default | Description |
| --- | --- | --- |
| `Name` | *(constructor)* | Identifies the pool and names its worker threads. |
| `WorkerCountMin` | `1` | Minimum number of idle workers retained by the pool. Must be 0 or greater and cannot exceed `WorkerCountMax`. |
| `WorkerCountMax` | `Environment.ProcessorCount` | Maximum number of workers the pool may create. Must be 1 or greater and cannot be less than `WorkerCountMin`. |
| `IdleTime` | `2.5` minutes | How long an inactive worker may be retained before it is removed. |
| `TryWorkDelay` | `10` | Delay in milliseconds before retrying when every worker is busy. Must be 1 or greater. |

```csharp
ISTAThreadPool pool = new STAThreadPool("shell") {
    WorkerCountMax = 4,
    WorkerCountMin = 1,
    IdleTime = TimeSpan.FromMinutes(1),
};
```

Values outside the documented ranges throw `ArgumentOutOfRangeException` and leave the property
unchanged. When `WorkerCountMin` and `WorkerCountMax` are both being raised, set `WorkerCountMax`
first; when both are being lowered, set `WorkerCountMin` first.

The pool's worker retention and scheduling settings live in a `STAThreadPoolOptions` record,
exposed by the pool through its `Options` property. The constructor optionally accepts a
`STAThreadPoolOptions` and stores a copy of it, so later changes to the instance passed in are
not observed by the pool. Setting a property on the pool and setting the corresponding property
of `pool.Options` are equivalent; both update the configuration the running pool applies. A
custom record can be built with an object initializer or with a `with` expression:

```csharp
var options = new STAThreadPoolOptions {
    WorkerCountMax = 4,
};

ISTAThreadPool pool = new STAThreadPool("shell", options);
```

### Shutting down

```csharp
pool.Empty();
```

`Empty` removes every worker from the pool and asks its STA message loop to exit. A worker that is
running work at the time finishes that work first. The pool remains usable afterward and will create
new workers on the next `Work` call.

Workers are also retired automatically: a periodic sweep removes workers that have been idle longer
than `IdleTime`, never dropping the pool below `WorkerCountMin`. Worker threads are background
threads, so they never keep the process alive.

### Creating a pool through a factory

Rather than referencing the Windows implementation directly, a program can create the pool through
an `ISTAThreadPoolFactory` obtained from the composition framework, using the
`Brows.Win32.STAThreadPool.Composition` package for the factory contract and the
`Brows.Win32.STAThreadPool.Composition.Exports` package for its implementation. Creating a pool
does not start any threads; workers are created on demand, and each call to `CreateThreadPool` creates a
separate pool. The pool returned by the factory is used exactly as shown above.

```csharp
using Brows.Threading;

ISTAThreadPoolFactory factory = /* obtained from the composition framework */;

ISTAThreadPool pool = factory.CreateThreadPool("shell", new STAThreadPoolOptions {
    WorkerCountMax = 4,
});
```

### Getting a pool for a target

`ISTAThreadPoolProvider` lets a component obtain a pool associated with a target object. Supply your
own implementation and make it available to the composition framework; this repository supplies the
provider contract only. `Brows.Win32.STAThreadPool.Composition.Exports` supplies a factory, but no
provider implementation.

The composition framework creates a single instance of each provider implementation for the lifetime
of the program. The implementation decides how targets map to pools, whether pools are shared, how
null or unsupported targets are handled, and how returned pools are retained and emptied. The
provider's composition lifetime does not prescribe the lifetime of its pools. A provider that uses the
concrete Windows `STAThreadPool` requires Windows, as described above.

```csharp
using Brows.Threading;

ISTAThreadPoolProvider provider = /* your implementation obtained from composition */;
ISTAThreadPool pool = provider.GetThreadPool(shellItem);

var name = await pool.Work("get-name", () => {
    return shellItem.GetDisplayName();
}, cancellationToken);
```

## Thread safety

`STAThreadPool` is safe to use from multiple threads at once. Each worker runs one work item at a
time, so work dispatched to the same worker is serialized, but separate work items may run
concurrently on different workers.

## Building

```
dotnet build
dotnet test
```

## License

[MIT](LICENSE)
