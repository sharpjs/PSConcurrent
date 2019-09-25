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

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class TaskHostTests
    {
        private Mock<PSHost>              MockHost;
        private Mock<PSHostUserInterface> MockUI;
        private TaskHost                  TaskHost;

        [SetUp]
        public void SetUp()
        {
            MockHost = new Mock<PSHost>              (MockBehavior.Strict);
            MockUI   = new Mock<PSHostUserInterface> (MockBehavior.Loose);

            MockHost
                .SetupGet(h => h.UI)
                .Returns(MockUI.Object);

            TaskHost = new TaskHost(MockHost.Object, new ConsoleState(), 42);
        }

        [TearDown]
        public void TearDown()
        {
            MockHost.Verify();
        }

        [Test]
        public void InstanceId_Get()
        {
            var other = new TaskHost(MockHost.Object, new ConsoleState(), 123);

            TaskHost.InstanceId.Should().NotBeEmpty().And.NotBe(other.InstanceId);
            other   .InstanceId.Should().NotBeEmpty();
        }

        [Test]
        public void Name_Get()
        {
            TaskHost.Name.Should().Be("Invoke-Concurrent[42]");
        }

        [Test]
        public void Version_Get()
        {
           var version = typeof(TaskHost).Assembly.GetName().Version;

           TaskHost.Version.Should().Be(version);
        }

        [Test]
        public void UI_Get()
        {
            TaskHost.UI.Should().NotBeNull().And.BeAssignableTo<TaskHostUI>();
        }

        [Test]
        public void CurrentCulture_Get()
        {
            var culture = CultureInfo.GetCultureInfo("fr-CA");

            MockHost
                .SetupGet(h => h.CurrentCulture)
                .Returns(culture)
                .Verifiable();

            TaskHost.CurrentCulture.Should().BeSameAs(culture);
        }

        [Test]
        public void CurrentUICulture_Get()
        {
            var culture = CultureInfo.GetCultureInfo("fr-CA");

            MockHost
                .SetupGet(h => h.CurrentUICulture)
                .Returns(culture)
                .Verifiable();

            TaskHost.CurrentUICulture.Should().BeSameAs(culture);
        }

        [Test]
        public void PrivateData_Get()
        {
            var data = new PSObject();

            MockHost
                .SetupGet(h => h.PrivateData)
                .Returns(data)
                .Verifiable();

            TaskHost.PrivateData.Should().BeSameAs(data);
        }

        [Test]
        public void DebuggerEnabled_Get()
        {
            MockHost
                .SetupGet(h => h.DebuggerEnabled)
                .Returns(true)
                .Verifiable();

            TaskHost.DebuggerEnabled.Should().BeTrue();
        }

        [Test]
        public void DebuggerEnabled_Set()
        {
            MockHost
                .SetupSet(h => h.DebuggerEnabled = true)
                .Verifiable();

            TaskHost.DebuggerEnabled = true;
        }

        [Test]
        public void EnterNestedPrompt()
        {
            MockHost
                .Setup(h => h.EnterNestedPrompt())
                .Verifiable();

            TaskHost.EnterNestedPrompt();
        }

        [Test]
        public void ExitNestedPrompt()
        {
            MockHost
                .Setup(h => h.ExitNestedPrompt())
                .Verifiable();

            TaskHost.ExitNestedPrompt();
        }

        [Test]
        public void NotifyBeginApplication()
        {
            MockHost
                .Setup(h => h.NotifyBeginApplication())
                .Verifiable();

            TaskHost.NotifyBeginApplication();
        }

        [Test]
        public void NotifyEndApplication()
        {
            MockHost
                .Setup(h => h.NotifyEndApplication())
                .Verifiable();

            TaskHost.NotifyEndApplication();
        }

        [Test]
        public void SetShouldExit()
        {
            MockHost
                .Setup(h => h.SetShouldExit(42))
                .Verifiable();

            TaskHost.SetShouldExit(42);
        }
    }
}
