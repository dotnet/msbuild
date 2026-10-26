// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Wraps a <see cref="Timer"/> with separate operations to stop future callbacks and drain callbacks already in progress.
    /// </summary>
    private sealed class DrainableTimer : IDisposable
    {
        // Running -> StopRequested -> Disposing -> Disposed
        private const int Running = 0;
        private const int StopRequested = 1;
        private const int Disposing = 2;
        private const int Disposed = 3;

        private readonly TimerCallback _callback;

        // Timer.Dispose(WaitHandle) signals this after callbacks already in progress have completed.
        private readonly ManualResetEvent _callbacksDrained = new(false);
        private readonly object? _state;
        private readonly Timer _timer;
        private int _disposeState;

        /// <summary>
        ///  Indicates whether new callback execution should be suppressed.
        /// </summary>
        private bool IsStoppingOrDisposed => Volatile.Read(ref _disposeState) != Running;

        /// <summary>
        ///  Creates a timer that invokes the specified callback.
        /// </summary>
        /// <param name="callback">The callback to invoke.</param>
        /// <param name="state">The state supplied to <paramref name="callback"/>.</param>
        /// <param name="dueTime">The delay before the first callback, in milliseconds.</param>
        /// <param name="period">The interval between callbacks, in milliseconds.</param>
        public DrainableTimer(TimerCallback callback, object? state, int dueTime, int period)
        {
            _callback = callback;
            _state = state;

            _timer = new Timer(
                static timerState => ((DrainableTimer)timerState!).InvokeCallback(),
                state: this,
                dueTime,
                period);
        }

        /// <summary>
        ///  Stops future callbacks without waiting for callbacks already in progress.
        /// </summary>
        /// <remarks>
        ///  This method does not block and may be called while the timer callback is running.
        ///  Call <see cref="Dispose"/> later to wait for that callback to complete.
        /// </remarks>
        public void Stop()
        {
            if (Interlocked.CompareExchange(ref _disposeState, StopRequested, Running) == Running)
            {
                _timer.Dispose(_callbacksDrained);
            }
        }

        /// <summary>
        ///  Stops future callbacks and waits for callbacks already in progress to complete.
        /// </summary>
        /// <remarks>
        ///  This method must not be called from the timer callback because it waits for that callback to complete.
        ///  Use <see cref="Stop"/> when stopping the timer from a callback execution path.
        /// </remarks>
        public void Dispose()
        {
            Stop();

            if (Interlocked.CompareExchange(ref _disposeState, Disposing, StopRequested) != StopRequested)
            {
                return;
            }

            try
            {
                _callbacksDrained.WaitOne();
            }
            finally
            {
                _callbacksDrained.Dispose();
                Volatile.Write(ref _disposeState, Disposed);
            }
        }

        private void InvokeCallback()
        {
            if (IsStoppingOrDisposed)
            {
                return;
            }

            _callback(_state);
        }
    }
}
