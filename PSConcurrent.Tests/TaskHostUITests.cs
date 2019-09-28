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
using System.Management.Automation;
using System.Management.Automation.Host;
using FluentAssertions;
using Moq;
using NUnit.Framework;

namespace PSConcurrent
{
    using static TestExtensions;

    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TaskHostUITests
    {
        [Test]
        public void Header_Default()
        {
            using var my = new TestHarness(taskId: 42);

            my.TaskHostUI.Header.Should().Be("Task 42");
        }

        [Test]
        public void Header_Set_Null()
        {
            using var my = new TestHarness();

            my.TaskHostUI
                .Invoking(u => u.Header = null!)
                .Should().Throw<ArgumentNullException>()
                .Where(e => e.ParamName == "value");
        }

        [Test]
        public void Header_Set_NotNull()
        {
            using var my = new TestHarness();

            var header = nameof(Header_Set_NotNull);

            my.TaskHostUI.Header = header;
            my.TaskHostUI.Header.Should().BeSameAs(header);
        }

        [Test]
        public void RawUI()
        {
            using var my = new TestHarness();

            var rawUI = my.Mocks.Create<PSHostRawUserInterface>();

            my.MockUI
                .SetupGet(u => u.RawUI)
                .Returns(rawUI.Object)
                .Verifiable();

            my.TaskHostUI.RawUI.Should().BeSameAs(rawUI.Object);
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void SupportsVirtualTerminal(bool value)
        {
            using var my = new TestHarness();

            my.MockUI
                .SetupGet(u => u.SupportsVirtualTerminal)
                .Returns(value)
                .Verifiable();

            my.TaskHostUI.SupportsVirtualTerminal.Should().Be(value);
        }

        [Test]
        public void Write_Text_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.Write("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.Write("message");
        }

        [Test]
        public void Write_Text_ContinuingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.Write("message"))
                .Verifiable();

            my.TaskHostUI.Write("message");
        }

        [Test]
        public void Write_Text_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.Write("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.Write("message");
        }

        [Test]
        public void Write_TextInColor_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.Write(fg, bg, "[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.Write(fg, bg, "message");
        }

        [Test]
        public void Write_TextInColor_ContinuingLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.Write(fg, bg, "message"))
                .Verifiable();

            my.TaskHostUI.Write(fg, bg, "message");
        }

        [Test]
        public void Write_TextInColor_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.Write(fg, bg, "[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.Write(fg, bg, "message");
        }

        [Test]
        public void WriteLine_Bare_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteLine("[Task 1]: "))
                .Verifiable();

            my.TaskHostUI.WriteLine();
        }

        [Test]
        public void WriteLine_Bare_ContinuingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteLine(""))
                .Verifiable();

            my.TaskHostUI.WriteLine();
        }

        [Test]
        public void WriteLine_Bare_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteLine("[Task 1]: "))
                .Verifiable();

            my.TaskHostUI.WriteLine();
        }

        [Test]
        public void WriteLine_Text_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteLine("message");
        }

        [Test]
        public void WriteLine_Text_ContinuingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteLine("message"))
                .Verifiable();

            my.TaskHostUI.WriteLine("message");
        }

        [Test]
        public void WriteLine_Text_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteLine("message");
        }

        [Test]
        public void WriteLine_TextInColor_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteLine(fg, bg, "[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteLine(fg, bg, "message");
        }

        [Test]
        public void WriteLine_TextInColor_ContinuingLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteLine(fg, bg, "message"))
                .Verifiable();

            my.TaskHostUI.WriteLine(fg, bg, "message");
        }

        [Test]
        public void WriteLine_TextInColor_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            var fg = Random.NextEnum<ConsoleColor>();
            var bg = Random.NextEnum<ConsoleColor>();

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteLine(fg, bg, "[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteLine(fg, bg, "message");
        }

        [Test]
        public void WriteDebugLine_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteDebugLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteDebugLine("message");
        }

        [Test]
        public void WriteDebugLine_ContinuingLine()
        {
            // TODO: This behavior probably is not desired.

            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteDebugLine("message"))
                .Verifiable();

            my.TaskHostUI.WriteDebugLine("message");
        }

        [Test]
        public void WriteDebugLine_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteDebugLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteDebugLine("message");
        }

        [Test]
        public void WriteVerboseLine_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteVerboseLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteVerboseLine("message");
        }

        [Test]
        public void WriteVerboseLine_ContinuingLine()
        {
            // TODO: This behavior probably is not desired.

            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteVerboseLine("message"))
                .Verifiable();

            my.TaskHostUI.WriteVerboseLine("message");
        }

        [Test]
        public void WriteVerboseLine_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteVerboseLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteVerboseLine("message");
        }

        [Test]
        public void WriteWarningLine_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteWarningLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteWarningLine("message");
        }

        [Test]
        public void WriteWarningLine_ContinuingLine()
        {
            // TODO: This behavior probably is not desired.

            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteWarningLine("message"))
                .Verifiable();

            my.TaskHostUI.WriteWarningLine("message");
        }

        [Test]
        public void WriteWarningLine_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteWarningLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteWarningLine("message");
        }

        [Test]
        public void WriteErrorLine_BeginningLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol = true;

            my.MockUI
                .Setup(u => u.WriteErrorLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteErrorLine("message");
        }

        [Test]
        public void WriteErrorLine_ContinuingLine()
        {
            // TODO: This behavior probably is not desired.

            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId;

            my.MockUI
                .Setup(u => u.WriteErrorLine("message"))
                .Verifiable();

            my.TaskHostUI.WriteErrorLine("message");
        }

        [Test]
        public void WriteErrorLine_InterruptingLine()
        {
            using var my = new TestHarness(taskId: 1);

            my.ConsoleState.IsAtBol    = false;
            my.ConsoleState.LastTaskId = my.TaskId + 42; // not me

            my.MockUI
                .Setup(u => u.WriteLine())
                .Verifiable();

            my.MockUI
                .Setup(u => u.WriteErrorLine("[Task 1]: message"))
                .Verifiable();

            my.TaskHostUI.WriteErrorLine("message");
        }

        [Test]
        public void WriteInformation()
        {
            using var my = new TestHarness();

            var record = new InformationRecord(new object(), "any");

            my.MockUI
                .Setup(u => u.WriteInformation(record))
                .Verifiable();

            my.TaskHostUI.WriteInformation(record);
        }

        [Test]
        public void WriteProgress()
        {
            using var my = new TestHarness();

            var activityId = Random.Next();
            var sourceId   = Random.Next();

            var record = new ProgressRecord(activityId, "Testing", "Testing WriteProgress");

            my.MockUI
                .Setup(u => u.WriteProgress(sourceId, record))
                .Verifiable();

            my.TaskHostUI.WriteProgress(sourceId, record);
        }

        private class TestHarness : TestHarnessBase
        {
            public TaskHostUI                TaskHostUI   { get; }
            public int                       TaskId       { get; }
            public Mock<PSHostUserInterface> MockUI       { get; }
            public ConsoleState              ConsoleState { get; }

            public TestHarness(int? taskId = null)
            {
                TaskId = taskId ?? Random.Next();
                MockUI = Mocks.Create<PSHostUserInterface>();

                ConsoleState = new ConsoleState
                {
                    IsAtBol    = Random.NextBool(),
                    LastTaskId = Random.Next()
                };

                TaskHostUI = new TaskHostUI(MockUI.Object, ConsoleState, TaskId);
            }
        }
    }
}
