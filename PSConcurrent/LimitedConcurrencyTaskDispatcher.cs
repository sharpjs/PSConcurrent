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
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Tasks.TaskCreationOptions;

namespace PSConcurrent
{
    internal class LimitedConcurrencyTaskDispatcher
    {
        // Queue of tasks waiting to be started
        private readonly BlockingCollection<Task> _queue;

        // Task that dequeues and starts tasks
        private readonly Task _dispatcher;

        // Propagates a cancellation request
        private readonly CancellationToken _cancellation;

        public LimitedConcurrencyTaskDispatcher(
            int               concurrency,
            CancellationToken cancellationToken = default)
        {
            if (concurrency < 1)
                throw new ArgumentOutOfRangeException(nameof(concurrency));

            MaxConcurrency = concurrency;
            _queue         = new BlockingCollection<Task>();
            _cancellation  = cancellationToken;
            _dispatcher    = Task.Factory.StartNew(Dispatch);
        }

        /// <summary>
        ///   Gets the maximum number of tasks that will be started.
        /// </summary>
        public int MaxConcurrency { get; }

        /// <summary>
        ///   Occurs when <see cref="CompleteAdding"/> has been called and all
        ///   added tasks have completed.
        /// </summary>
        public event EventHandler Completed;

        /// <summary>
        ///   Adds the specified task to the queue.
        /// </summary>
        /// <param name="task">The task to add.</param>
        public void Add(Task task)
        {
            _queue.Add(task);
        }

        /// <summary>
        ///   Marks the queue as not accepting any further addition.
        /// </summary>
        public void CompleteAdding()
        {
            _queue.CompleteAdding();
        }

        private void Dispatch()
        {
            using (var semaphore = CreateSemaphore())
            {
                var tasks = new List<Task>();

                // Dequeue and start tasks until either all have been started
                // or cancellation has been requested.
                while (_queue.TryTake(out var task, Timeout.InfiniteTimeSpan))
                {
                    // A task has been dequeued.  Wait until the concurrency
                    // level is appropriate for starting the task.
                    semaphore.Wait();

                    // A change in the concurrency level might be a result of
                    // cancellation.  In that case, do not start the task.
                    if (_cancellation.IsCancellationRequested)
                        return;

                    // Attempt to start the task.
                    try
                    {
                        task.Start();
                    }
                    catch (InvalidOperationException)
                    {
                        // Task was not in a valid state to be started.  It
                        // could have been canceled after the above check.
                        // Update the concurrency level.
                        semaphore.Release();
                        continue;
                    }

                    // The task is started.  Update the concurrency level after
                    // it completes.
                    task = task.ContinueWith(_ => semaphore.Release());

                    // Remember task so that it can be awaited.
                    tasks.Add(task);
                }

                // Wait for all started tasks to complete.
                Task.WaitAll(tasks.ToArray());
            }

            // Notify observers of completion.
            OnCompleted();
        }

        private SemaphoreSlim CreateSemaphore()
        {
            return new SemaphoreSlim(
                initialCount: MaxConcurrency,
                maxCount:     MaxConcurrency
            );
        }

        private void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
