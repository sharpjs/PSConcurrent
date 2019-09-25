/*
    Copyright (C) 2019 Jeffrey Sharp

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

namespace PSConcurrent
{
    /// <summary>
    ///   A simple message-loop-style dispatcher that defines a main thread and
    ///   enables other threads to invoke actions synchronously on the main
    ///   thread.
    /// </summary>
    /// <remarks>
    ///   This type is a slimmed-down <c>SynchronizationContext</c>.
    /// </remarks>
    internal class MainThreadDispatcher
    {
        // Queue of actions waiting to be executed on the main thread
        private readonly BlockingCollection<Thunk>
            _queue = new BlockingCollection<Thunk>();

        /// <summary>
        ///   Invokes an action synchronously on the main thread.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        /// <remarks>
        ///   This method must NOT be invoked from the main thread.
        ///   Otherwise, a deadlock will occur.
        /// </remarks>
        public void InvokeOnMainThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (_queue.IsAddingCompleted)
                throw new InvalidAsynchronousStateException(
                    "Failed to invoke an action on the main thread, " +
                    "because the main-thread dispatch loop has completed."
                );

            using (var done = new ManualResetEventSlim())
            {
                _queue.Add(new Thunk(action, done));
                done.Wait();
            }
        }

        /// <summary>
        ///   Runs the main-thread dispatch loop, invoking on the current thread
        ///   any actions passed to <see cref="InvokeOnMainThread(Action)"/>,
        ///   until <see cref="Complete"/> is called.
        /// </summary>
        /// <remarks>
        ///   This method is thread-safe.  The thread on which this method is
        ///   invoked becomes the main thread.
        /// </remarks>
        public void Run()
        {
            while (_queue.TryTake(out var thunk, Timeout.InfiniteTimeSpan))
                thunk.Invoke();
        }

        /// <summary>
        ///   Causes the main-thread dispatch loop to complete.
        ///   <see cref="Run"/> will return.
        /// </summary>
        /// <remarks>
        ///   This method is thread-safe.
        /// </remarks>
        public void Complete()
        {
            _queue.CompleteAdding();
        }

        private readonly struct Thunk
        {
            private readonly Action               _action;
            private readonly ManualResetEventSlim _done;

            public Thunk(Action action, ManualResetEventSlim done)
            {
                _action = action;
                _done   = done;
            }

            public void Invoke()
            {
                try
                {
                    _action();
                }
                finally
                {
                    _done.Set();
                }
            }
        }
    }
}
