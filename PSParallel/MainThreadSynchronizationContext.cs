/*
    Copyright (C) 2017 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallel
{
    internal class MainThreadSynchronizationContext : SynchronizationContext
    {
        // Indicates no main thread yet
        private const int NoThread = 0;

        // Queue of delegates waiting to be executed on the main thread
        private readonly BlockingCollection<Thunk>
            _queue = new BlockingCollection<Thunk>();

        private int _mainThreadId;      // ThreadId of main thread
        private int _operationCount;    // Count of async void methods running

        /// <summary>
        ///   Gets whether the current thread is the main thread of this
        ///   <c>GeneralSynchronizationContext</c>.
        /// </summary>
        public bool IsInMainThread
            => _mainThreadId == Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        ///   Returns the same synchronization context.
        ///   Copying is not required for this implementation.
        /// </summary>
        /// <returns>The synchronization context.</returns>
        public override SynchronizationContext CreateCopy()
            => this;

        /// <summary>
        ///   Invokes an action synchronously on the main thread.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        /// <param name="state">An object to pass as argument to <paramref name="action"/>.</param>
        public override void Send(SendOrPostCallback action, object state)
        {
            RequireAction(action);
            RequireNotEnded();

            if (IsInMainThread)
            {
                //action(state);
                throw new NotSupportedException("Reentrancy is not supported.");
            }
            else
            {
                var done = new ManualResetEventSlim();
                _queue.Add(new Thunk(action, state, done));
                done.Wait();
            }
        }

        /// <summary>
        ///   Invokes an action asynchronously on the main thread.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        /// <param name="state">An object to pass as argument to <paramref name="action"/>.</param>
        public override void Post(SendOrPostCallback action, object state)
        {
            RequireAction(action);
            RequireNotEnded();

            _queue.Add(new Thunk(action, state));
        }

        /// <summary>
        ///   Runs the main-thread loop, invoking delegates sent with
        ///   <see cref="Send(SendOrPostCallback, object)"/> or
        ///   <see cref="Post(SendOrPostCallback, object)"/> on this thread
        ///   until <see cref="Complete"/> is called.
        /// </summary>
        public void RunMainThread()
            => RunMainThread(Timeout.InfiniteTimeSpan);

        /// <summary>
        ///   Runs the main-thread loop, invoking delegates sent with
        ///   <see cref="Send(SendOrPostCallback, object)"/> or
        ///   <see cref="Post(SendOrPostCallback, object)"/> on this thread
        ///   until either <see cref="Complete"/> is called or
        ///   <paramref name="wait"/> time elapses with no delegate to invoke.
        /// </summary>
        /// <param name="wait">The maximum amount of time to wait for a delegate to execute.</param>
        public void RunMainThread(TimeSpan wait)
        {
            SetMainThread();

            while (_queue.TryTake(out var thunk, wait))
                thunk.Invoke();
        }

        /// <summary>
        ///   Informs the synchronization context that an asynchronous operation
        ///   (usually an <c>async void</c> method invocation) has started.
        /// </summary>
        /// <remarks>
        ///   This method is thread-safe.
        /// </remarks>
        public override void OperationStarted()
        {
            Interlocked.Increment(ref _operationCount);
        }

        /// <summary>
        ///   Informs the synchronization context that an asynchronous operation
        ///   (usually an <c>async void</c> method invocation) has completed.
        ///   If no asynchronous operations remain in progress, the context
        ///   completes and <see cref="RunMainThread"/> returns.
        /// </summary>
        /// <remarks>
        ///   This method is thread-safe.
        /// </remarks>
        public override void OperationCompleted()
        {
            if (Interlocked.Decrement(ref _operationCount) <= 0)
                Complete();
        }

        /// <summary>
        ///   Causes the synchronization context to complete.
        ///   <see cref="RunMainThread"/> will return.
        /// </summary>
        /// <remarks>
        ///   This method is thread-safe.
        /// </remarks>
        public void Complete()
        {
            _mainThreadId = NoThread;
            _queue.CompleteAdding();
        }

        public static T Run<T>(Func<Task<T>> action)
        {
            var context = new MainThreadSynchronizationContext();

            using (new SynchronizationScope(context))
            {
                var task = action();
                task.ContinueWith(_ => context.Complete());
                context.RunMainThread();
                return task.Result;
            }
        }

        public static void Run(Action action)
        {
            var context = new MainThreadSynchronizationContext();

            using (new SynchronizationScope(context))
            {
                context.OperationStarted();
                action();
                context.OperationCompleted();
                context.RunMainThread();
            }
        }

        private void SetMainThread()
        {
            var current  = Thread.CurrentThread.ManagedThreadId;
            var previous = Interlocked.CompareExchange(
                ref _mainThreadId, current, comparand: NoThread);

            if (previous == NoThread || previous == current)
                // This thread either just became the main thread, or it already
                // was the main thread (in case of reentrancy).
                return;

            throw new InvalidOperationException(
                "Another thread is already the main thread of this event loop.  " +
                "Run should be called from the main thread only."
            );
        }

        private void RequireNotEnded()
        {
            if (!_queue.IsAddingCompleted)
                return;

            throw new InvalidAsynchronousStateException(
                "Failed to invoke a method on the main thread, " +
                "because the destination event loop has exited."
            );
        }

        private static void RequireAction(SendOrPostCallback action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
        }

        private class Thunk
        {
            private readonly SendOrPostCallback   _action;
            private readonly object               _state;
            private readonly ManualResetEventSlim _done;

            public Thunk(
                SendOrPostCallback   action,
                object               state,
                ManualResetEventSlim done = null)
            {
                _action = action;
                _state  = state;
                _done   = done;
            }

            public void Invoke()
            {
                try
                {
                    _action(_state);
                }
                finally
                {
                    _done?.Set();
                }
            }
        }
    }
}
