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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class LimitedConcurrencyTaskSchedulerTests
    {
        private static readonly int
            CoreCount = Environment.ProcessorCount;

        private static readonly TimeSpan
            SleepTime = 3.Seconds(),
            GraceTime = 1.Seconds();

        [Test]
        public void Construct_ZeroConcurrency()
        {
            (0).Invoking(n => new LimitedConcurrencyTaskScheduler(n))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Construct_NegativeConcurrency()
        {
            (-1).Invoking(n => new LimitedConcurrencyTaskScheduler(n))
                .Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void MaximumConcurrencyLevel()
        {
            var scheduler = new LimitedConcurrencyTaskScheduler(42);

            scheduler.MaximumConcurrencyLevel.Should().Be(42);
        }

        [Test, Retry(3)]
        public void Run_UpToConcurrencyLimit()
        {
            var scheduler  = new LimitedConcurrencyTaskScheduler(CoreCount);
            var tasks      = CreateTasks(CoreCount, SleepThenReturnStartTime);
            var baseline   = DateTime.UtcNow;

            var startTimes = RunTasks(tasks, scheduler);

            startTimes.Count(t => t < baseline + GraceTime).Should().Be(CoreCount);
        }

        [Test, Retry(3)]
        public void Run_OverConcurrencyLimit()
        {
            var scheduler  = new LimitedConcurrencyTaskScheduler(CoreCount);
            var tasks      = CreateTasks(CoreCount + 1, SleepThenReturnStartTime);
            var baseline   = DateTime.UtcNow;

            var startTimes = RunTasks(tasks, scheduler);

            startTimes.Count(t => t < baseline             + GraceTime).Should().Be(CoreCount    );
            startTimes.Count(t => t < baseline + SleepTime + GraceTime).Should().Be(CoreCount + 1);
        }

        private static DateTime SleepThenReturnStartTime()
        {
            var startTime = DateTime.UtcNow;
            Thread.Sleep(SleepTime);
            return startTime;
        }

        private static Task<T>[] CreateTasks<T>(int count, Func<T> action)
        {
            return Enumerable
                .Range(0, count)
                .Select(_ => new Task<T>(action))
                .ToArray();
        }

        private static T[] RunTasks<T>(Task<T>[] tasks, LimitedConcurrencyTaskScheduler scheduler)
        {
            scheduler.CurrentConcurrencyLevel.Should().Be(0);

            foreach (var task in tasks)
                task.Start(scheduler);

            scheduler.GetScheduledTasksInternal().Should().BeEquivalentTo(tasks);
            scheduler.CurrentConcurrencyLevel.Should().Be(scheduler.MaximumConcurrencyLevel);

            Task.WaitAll(tasks);

            scheduler.CurrentConcurrencyLevel.Should().Be(0);

            return tasks
                .Select(t => t.Result)
                .ToArray();
        }

        private static void AllShouldBeWithinGraceTime(DateTime[] times, DateTime min, TimeSpan grace)
        {
            foreach (var time in times)
                time.Should().BeWithin(grace).After(min);
        }
    }
}
