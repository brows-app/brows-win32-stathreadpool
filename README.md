# brows-win32-stathreadpool

STA thread pool for .NET on Win32.

Some Windows APIs — shell namespace extensions, many COM servers, drag-and-drop, and the clipboard
among them — must be called from a thread in a single-threaded apartment that pumps messages. The
.NET thread pool is MTA, so this library provides a small pool of reusable STA worker threads and an
`async`-friendly way to run work on them.

## Installation

```
dotnet add package Brows.Win32.STAThreadPool
```

Target frameworks: `net462`, `net48`, `net8.0-windows`, `net10.0-windows`. On .NET 8 and later the
consuming project must target a Windows TFM, such as `net10.0-windows`; the Windows Desktop
framework reference the STA message loop needs flows in from this package.

## Usage

Create one pool and keep it for the lifetime of the component that needs STA access. Creating a pool
does not start any threads; workers are created on demand.

```csharp
using Brows.Threading;

var pool = new STAThreadPool("shell");

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
| `WorkerCountMin` | `1` | Minimum number of workers retained by the pool. Must be 1 or greater and cannot exceed `WorkerCountMax`. |
| `WorkerCountMax` | `16` | Maximum number of workers the pool may create. Must be 1 or greater and cannot be less than `WorkerCountMin`. |
| `IdleTime` | `2.5` minutes | How long an inactive worker may be retained before it is removed. |
| `TryWorkDelay` | `10` | Delay in milliseconds before retrying when every worker is busy. Must be 1 or greater. |

```csharp
var pool = new STAThreadPool("shell") {
    WorkerCountMax = 4,
    WorkerCountMin = 1,
    IdleTime = TimeSpan.FromMinutes(1),
};
```

Values outside the documented ranges throw `ArgumentOutOfRangeException` and leave the property
unchanged. When `WorkerCountMin` and `WorkerCountMax` are both being raised, set `WorkerCountMax`
first; when both are being lowered, set `WorkerCountMin` first.

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
