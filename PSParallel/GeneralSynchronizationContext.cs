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
using System.Threading;
using System.Threading.Tasks;

namespace PSParallel
{
    internal class GeneralSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<Thunk>
            _queue = new BlockingCollection<Thunk>();

        // Special pseudo ThreadIds
        private const int
            Initial =  0,   // Initial state; Run() not yet called
            Ended   = -1;   // Run() ended

        private int _mainThreadId;

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
                action(state);
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

        public void Run(CancellationToken cancellationToken)
        {
            EnterRunningState();

            try
            {
                while (_queue.TryTake(out var thunk, Timeout.Infinite, cancellationToken))
                    thunk.Invoke();
            }
            finally
            {
                _mainThreadId = Ended;
            }
        }

        public static T Run<T>(Func<Task<T>> action)
        {
            var previousContext = Current;
            try
            {
                var context = new GeneralSynchronizationContext();
                SetSynchronizationContext(context);

                var task = action();

                var cancellation = new CancellationTokenSource();
                task.ContinueWith(_ => cancellation.Cancel());

                context.Run(cancellation.Token);

                return task.Result;
            }
            finally
            {
                SetSynchronizationContext(previousContext);
            }
        }

        private void EnterRunningState()
        {
            var previous = Interlocked.CompareExchange(
                ref _mainThreadId,
                Thread.CurrentThread.ManagedThreadId,
                comparand: Initial
            );

            if (previous == Initial)
                return;

            throw new InvalidOperationException(
                "The synchronization context is already running."
            );
        }

        private void EnterEndedState()
        {
            _mainThreadId = Ended;
        }

        private static void RequireAction(SendOrPostCallback action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
        }

        private void RequireNotEnded()
        {
            if (_mainThreadId != Ended)
                return;

            throw new InvalidOperationException(
                "The synchronization context has ended.  " +
                "Cannot post further messages to its event loop."
            );
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
