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
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class LimitedConcurrencyTaskDispatcherTests
    {
        [Test]
        public void ConcurrencyUpToProcessorCount()
        {
            var concurrency = Environment.ProcessorCount;
            var dispatcher  = new LimitedConcurrencyTaskDispatcher(concurrency);

            var creationTime   = DateTime.UtcNow;
            var completionTime = DateTime.MaxValue;
            var taskStartTimes = new ConcurrentBag<DateTime>();

            // Add tasks
            for (var i = 0; i < concurrency; i++)
            {
                dispatcher.Add(new Task(() =>
                {
                    taskStartTimes.Add(DateTime.UtcNow);
                    Thread.Sleep(5.Seconds());
                }));
            }

            // Wait for completion
            using (var completion = new ManualResetEventSlim())
            {
                dispatcher.Completed += (sender, args) =>
                {
                    completionTime = DateTime.UtcNow;
                    completion.Set();
                };

                dispatcher.CompleteAdding();
                completion.Wait();
            }

            // Verify that tasks executed concurrently
            var giveOrTake             = 2.Seconds();
            var maximumTaskStartTime   = creationTime + giveOrTake;
            var expectedCompletionTime = creationTime + 5.Seconds();

            completionTime.Should().BeCloseTo(expectedCompletionTime, giveOrTake);

            taskStartTimes.Should().OnlyContain(t => t < maximumTaskStartTime);
        }
    }
}
