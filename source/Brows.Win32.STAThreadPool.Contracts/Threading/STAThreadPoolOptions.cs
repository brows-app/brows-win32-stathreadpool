using System;

namespace Brows.Threading;

/// <summary>
/// Specifies worker retention and scheduling options for an STA thread pool.
/// </summary>
/// <remarks>
/// This type is part of the platform-neutral <c>Brows.Win32.STAThreadPool.Contracts</c> package. On .NET 8 and
/// later, it can be used from platform-neutral target frameworks without Windows. It stores and validates
/// settings; it does not create workers.
/// </remarks>
public sealed record STAThreadPoolOptions {
    /// <summary>
    /// Gets or sets the length of time an inactive worker may be retained before it is removed.
    /// The default is 2.5 minutes.
    /// </summary>
    public TimeSpan IdleTime { get; set; } = TimeSpan.FromMinutes(2.5);

    /// <summary>
    /// Gets or sets the delay, in milliseconds, before retrying when all workers are busy.
    /// The value must be 1 or greater. The default is 10 milliseconds.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is less than 1.
    /// </exception>
    public int TryWorkDelay {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(TryWorkDelay),
                    actualValue: value,
                    message: $"The value of '{nameof(TryWorkDelay)}' must be 1 or greater.");
            }
            field = value;
        }
    } = 10;

    /// <summary>
    /// Gets or sets the maximum number of workers that can be created.
    /// The value must be 1 or greater and cannot be less than <see cref="WorkerCountMin"/>.
    /// The default is <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is less than 1 or less than <see cref="WorkerCountMin"/>.
    /// </exception>
    public int WorkerCountMax {
        get;
        set {
            if (value < 1) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMax),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMax)}' must be 1 or greater.");
            }
            if (value < WorkerCountMin) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMax),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMax)}' cannot be less than " +
                             $"the value of '{nameof(WorkerCountMin)}'.");
            }
            field = value;
        }
    } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the minimum number of idle workers retained by the pool.
    /// The value must be 0 or greater and cannot exceed <see cref="WorkerCountMax"/>.
    /// The default is 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The assigned value is less than 0 or greater than <see cref="WorkerCountMax"/>.
    /// </exception>
    public int WorkerCountMin {
        get;
        set {
            if (value < 0) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMin),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMin)}' must be 0 or greater.");
            }
            /*
             * Property initializers run in declaration order, so this setter can run
             * while the backing field of WorkerCountMax still holds its uninitialized
             * value of 0, which is never a valid WorkerCountMax. Skip the cross-check
             * in that state; when WorkerCountMax is assigned, whether here or by its
             * own initializer, its setter enforces the same pair constraint against
             * this value.
             */
            var exceedsAssignedMax = WorkerCountMax > 0 && value > WorkerCountMax;
            if (exceedsAssignedMax) {
                throw new ArgumentOutOfRangeException(
                    paramName: nameof(WorkerCountMin),
                    actualValue: value,
                    message: $"The value of '{nameof(WorkerCountMin)}' cannot be greater than " +
                             $"the value of '{nameof(WorkerCountMax)}'.");
            }
            field = value;
        }
    } = 1;
}
