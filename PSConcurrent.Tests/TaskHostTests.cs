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

using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace PSConcurrent
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TaskHostTests
    {
        [Test]
        public void InstanceId_Get()
        {
            using var my0 = new TestHarness();
            using var my1 = new TestHarness();

            my0.TaskHost.InstanceId.Should().NotBeEmpty();
            my1.TaskHost.InstanceId.Should().NotBeEmpty().And.NotBe(my0.TaskHost.InstanceId);
        }

        [Test]
        public void Name_Get()
        {
            using var my = new TestHarness(taskId: 42);

            my.TaskHost.Name.Should().Be("Invoke-Concurrent[42]");
        }

        [Test]
        public void Version_Get()
        {
            using var my = new TestHarness();

            var version = typeof(TaskHost).Assembly.GetName().Version;

            my.TaskHost.Version.Should().Be(version);
        }

        [Test]
        public void UI_Get()
        {
            using var my = new TestHarness();

            my.TaskHost.UI.Should().BeOfType<TaskHostUI>();
        }

        [Test]
        public void CurrentCulture_Get()
        {
            using var my = new TestHarness();

            var culture = CultureInfo.GetCultureInfo("fr-CA");

            my.MockHost
                .SetupGet(h => h.CurrentCulture)
                .Returns(culture)
                .Verifiable();

            my.TaskHost.CurrentCulture.Should().BeSameAs(culture);
        }

        [Test]
        public void CurrentUICulture_Get()
        {
            using var my = new TestHarness();

            var culture = CultureInfo.GetCultureInfo("fr-CA");

            my.MockHost
                .SetupGet(h => h.CurrentUICulture)
                .Returns(culture)
                .Verifiable();

            my.TaskHost.CurrentUICulture.Should().BeSameAs(culture);
        }

        [Test]
        public void PrivateData_Get()
        {
            using var my = new TestHarness();

            var data = new PSObject();

            my.MockHost
                .SetupGet(h => h.PrivateData)
                .Returns(data)
                .Verifiable();

            my.TaskHost.PrivateData.Should().BeSameAs(data);
        }

        [Test]
        public void DebuggerEnabled_Get()
        {
            using var my = new TestHarness();

            my.MockHost
                .SetupGet(h => h.DebuggerEnabled)
                .Returns(true)
                .Verifiable();

            my.TaskHost.DebuggerEnabled.Should().BeTrue();
        }

        [Test]
        public void DebuggerEnabled_Set()
        {
            using var my = new TestHarness();

            my.MockHost
                .SetupSet(h => h.DebuggerEnabled = true)
                .Verifiable();

            my.TaskHost.DebuggerEnabled = true;
        }

        [Test]
        public void EnterNestedPrompt()
        {
            using var my = new TestHarness();

            my.MockHost
                .Setup(h => h.EnterNestedPrompt())
                .Verifiable();

            my.TaskHost.EnterNestedPrompt();
        }

        [Test]
        public void ExitNestedPrompt()
        {
            using var my = new TestHarness();

            my.MockHost
                .Setup(h => h.ExitNestedPrompt())
                .Verifiable();

            my.TaskHost.ExitNestedPrompt();
        }

        [Test]
        public void NotifyBeginApplication()
        {
            using var my = new TestHarness();

            my.MockHost
                .Setup(h => h.NotifyBeginApplication())
                .Verifiable();

            my.TaskHost.NotifyBeginApplication();
        }

        [Test]
        public void NotifyEndApplication()
        {
            using var my = new TestHarness();

            my.MockHost
                .Setup(h => h.NotifyEndApplication())
                .Verifiable();

            my.TaskHost.NotifyEndApplication();
        }

        [Test]
        public void SetShouldExit()
        {
            using var my = new TestHarness();

            my.MockHost
                .Setup(h => h.SetShouldExit(42))
                .Verifiable();

            my.TaskHost.SetShouldExit(42);
        }

        private class TestHarness : TestHarnessBase
        {
            public TaskHost                  TaskHost { get; }
            public Mock<PSHost>              MockHost { get; }
            public Mock<PSHostUserInterface> MockUI   { get; }

            public TestHarness(int taskId = 42)
            {
                MockHost = Mocks.Create<PSHost>();
                MockUI   = Mocks.Create<PSHostUserInterface>(MockBehavior.Loose);

                MockHost
                    .SetupGet(h => h.UI)
                    .Returns(MockUI.Object);

                TaskHost = new TaskHost(MockHost.Object, new ConsoleState(), taskId);
            }
        }
    }
}
