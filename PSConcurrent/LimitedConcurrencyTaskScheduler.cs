/*
    Copyright (C) 2018 Jeffrey Sharp

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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PSConcurrent
{
    /// <summary>
    ///   A <c>TaskScheduler</c> that limits the number of concurrently
    ///   executing tasks.
    /// </summary>
    /// <remarks>
    ///   This scheduler improves upon the implementation published as an
    ///   example in <a href="https://docs.microsoft.com/en-ca/dotnet/api/system.threading.tasks.taskscheduler?view=netframework-4.7.1#examples">the
    ///   .NET Framework documentation for <c>TaskScheduler</c></a>.  In
    ///   contrast to the example, this scheduler is a lock-free implementation
    ///   using <c>System.Collections.Concurrent</c> collections and
    ///   <c>System.Threading.Interlocked</c> operations.  This scheduler also
    ///   provides the <see cref="CurrentConcurrencyLevel"/> property, which
    ///   returns the instantaneous number of concurrently executing tasks.
    /// </remarks>
    internal class LimitedConcurrencyTaskScheduler : TaskScheduler
    {
        [ThreadStatic]
        // Whether the current thread is running the dispatcher loop
        private static bool _threadIsDispatching;

        // Tasks waiting to be executed
        private readonly ConcurrentQueue<Entry> _queue;

        // Concurrency level
        private          int _dispatcherCount; // current level
        private readonly int _dispatcherLimit; // maximum level

        /// <summary>
        ///   Creates an instance of the
        ///   <see cref="LimitedConcurrencyTaskScheduler"/> class.
        /// </summary>
        /// <param name="concurrency">
        ///   The maximum number of concurrently executing tasks to allow.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///   <paramref name="concurrency"/> is less than <c>1</c>.
        /// </exception>
        public LimitedConcurrencyTaskScheduler(int concurrency)
        {
            if (concurrency < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrency));

            _queue           = new ConcurrentQueue<Entry>();
            _dispatcherLimit = concurrency;
        }

        /// <summary>
        ///   The current number of concurrently executing tasks attached to
        ///   the scheduler.
        /// </summary>
        public int CurrentConcurrencyLevel
        {
            get => _dispatcherCount;
        }

        /// <summary>
        ///   The maximum allowed number of concurrently executing tasks
        ///   attached to the scheduler.
        /// </summary>
        public sealed override int MaximumConcurrencyLevel
        {
            get => _dispatcherLimit;
        }

        /// <inheritdoc/>
        protected override void QueueTask(Task task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            _queue.Enqueue(new Entry(task));
            TryStartDispatcher();
        }

        /// <inheritdoc/>
        protected override bool TryDequeue(Task task)
        {
            if (task == null)
                return false;

            // ConcurrentQueue does not support removal of arbitrary entries.
            // Instead, take the task out of the entry, leaving an empty entry.
            return _queue.Any(e => e.TryTake(task));
        }

        /// <inheritdoc/>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // Provide a semi-atomic snapshot for debug purposes.  The queue
            // enumeration is atomic, but the task projection is not.
            return _queue.Select(e => e.Task).Where(t => t != null).ToList();
        }

        // Test point
        internal IEnumerable<Task> GetScheduledTasksInternal()
            => GetScheduledTasks();

        /// <inheritdoc/>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (task == null)
                return false;

            // Should inline a task only from a dispatcher thread
            if (!_threadIsDispatching)
                return false;

            // When inlining a task, ensure it is removed from the queue
            if (taskWasPreviouslyQueued && !TryDequeue(task))
                return false;

            return TryExecuteTask(task);
        }

        // Starts a dispatcher loop if concurrency permits.
        private void TryStartDispatcher()
        {
            // Lock-free retry loop
            for (;;)
            {
                // Check if concurrency limit allows another dispatcher
                var count  = _dispatcherCount;
                if (count >= _dispatcherLimit)
                    return; // Concurrency limit reached

                // Attempt to increment concurrency level
                var replaced = Interlocked.CompareExchange(ref _dispatcherCount, count + 1, count);
                if (replaced != count)
                    continue; // Concurrency level changed since checked; retry

                // Start dispatcher
                ThreadPool.UnsafeQueueUserWorkItem(Dispatch, null);
                return;
            }
        }

        // Dispatcher loop.
        private void Dispatch(object arg /* unused */)
        {
            try
            {
                // Mark current thread as a dispatcher, so that tasks can be
                // inlined on the current thread by TryExecuteTaskInline.
                _threadIsDispatching = true;

                // Try to execute all entries in the queue.  Note that an entry
                // might be empty due to a previous TryDequeue invocation.
                while (_queue.TryDequeue(out var entry))
                    if (entry.TryTake(out var task))
                        TryExecuteTask(task);
            }
            finally
            {
                // Unmark current thread as a dispatcher.
                Interlocked.Decrement(ref _dispatcherCount);
                _threadIsDispatching = false;
            }
        }

        // An entry in the task queue.  ConcurrentQueue<T> does not implement
        // removal of arbitary items.  This type adds that capability.  A task
        // is removed from the queue by removing the task from its entry,
        // leaving an empty entry.  Empty entries should be skipped on dequeue.
        private sealed class Entry
        {
            private Task _task;

            public Entry(Task task)
                => _task = task;

            public Task Task
                => _task;

            public bool TryTake(out Task task)
                => (task = Take()) != null;

            public bool TryTake(Task task)
                => _task == task && Take() != null;

            private Task Take()
                => Interlocked.Exchange(ref _task, null);
        }
    }
}
