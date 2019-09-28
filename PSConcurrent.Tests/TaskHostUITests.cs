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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Net;
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeFalse();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
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

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
        }

        [Test]
        public void WriteInformation()
        {
            using var my = new TestHarness();

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var record = new InformationRecord(new object(), "any");

            my.MockUI
                .Setup(u => u.WriteInformation(record))
                .Verifiable();

            my.TaskHostUI.WriteInformation(record);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
        }

        [Test]
        public void WriteProgress()
        {
            using var my = new TestHarness();

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var activityId = Random.Next();
            var sourceId   = Random.Next();

            var record = new ProgressRecord(activityId, "Testing", "Testing WriteProgress");

            my.MockUI
                .Setup(u => u.WriteProgress(sourceId, record))
                .Verifiable();

            my.TaskHostUI.WriteProgress(sourceId, record);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
        }

        [Test]
        public void ReadLine()
        {
            using var my = new TestHarness(taskId: 1);

            var result = "result";

            my.MockUI
                .Setup(u => u.ReadLine())
                .Returns(result)
                .Verifiable();

            my.TaskHostUI.ReadLine().Should().BeSameAs(result);

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
        }

        [Test]
        public void ReadLineAsSecureString()
        {
            using var my = new TestHarness(taskId: 1);

            var result = new NetworkCredential("unused", "result").SecurePassword;

            my.MockUI
                .Setup(u => u.ReadLineAsSecureString())
                .Returns(result)
                .Verifiable();

            my.TaskHostUI.ReadLineAsSecureString().Should().BeSameAs(result);

            my.ConsoleState.IsAtBol   .Should().BeTrue();
            my.ConsoleState.LastTaskId.Should().Be(my.TaskId);
        }

        [Test]
        public void Prompt()
        {
            using var my = new TestHarness(taskId: 1);

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var caption  = "Test Prompt";
            var message  = "This is a test prompt.";
            var fields   = new Collection<FieldDescription>();
            var expected = new Dictionary<string, PSObject>();

            my.MockUI
                .Setup(u => u.Prompt(caption, message, fields))
                .Returns(expected)
                .Verifiable();

            var result = my.TaskHostUI.Prompt(caption, message, fields);

            result.Should().BeSameAs(expected);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
        }

        [Test]
        public void PromptForChoice()
        {
            using var my = new TestHarness(taskId: 1);

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var caption  = "Test Prompt";
            var message  = "This is a test prompt.";
            var choices  = new Collection<ChoiceDescription>();
            var @default = Random.Next();
            var expected = Random.Next();

            my.MockUI
                .Setup(u => u.PromptForChoice(caption, message, choices, @default))
                .Returns(expected)
                .Verifiable();

            var result = my.TaskHostUI.PromptForChoice(caption, message, choices, @default);

            result.Should().Be(expected);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
        }

        [Test]
        public void PromptForCredential()
        {
            using var my = new TestHarness(taskId: 1);

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var caption    = "Test Prompt";
            var message    = "This is a test prompt.";
            var userName   = "test";
            var targetName = "test.example.net";
            var expected   = new PSCredential(
                userName: "test", 
                password: new NetworkCredential("", "password").SecurePassword
            );

            my.MockUI
                .Setup(u => u.PromptForCredential(caption, message, userName, targetName))
                .Returns(expected)
                .Verifiable();

            var result = my.TaskHostUI.PromptForCredential(caption, message, userName, targetName);

            result.Should().Be(expected);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
        }

        [Test]
        public void PromptForCredential_WithTypesOptions()
        {
            using var my = new TestHarness(taskId: 1);

            var originalIsAtBol    = my.ConsoleState.IsAtBol;
            var originalLastTaskId = my.ConsoleState.LastTaskId;

            var caption    = "Test Prompt";
            var message    = "This is a test prompt.";
            var userName   = "test";
            var targetName = "test.example.net";
            var types      = Random.NextEnum<PSCredentialTypes>();
            var options    = Random.NextEnum<PSCredentialUIOptions>();
            var expected   = new PSCredential(
                userName: "test", 
                password: new NetworkCredential("", "password").SecurePassword
            );

            my.MockUI
                .Setup(u => u.PromptForCredential(
                    caption, message, userName, targetName, types, options
                ))
                .Returns(expected)
                .Verifiable();

            var result = my.TaskHostUI.PromptForCredential(
                caption, message, userName, targetName, types, options
            );

            result.Should().Be(expected);

            my.ConsoleState.IsAtBol   .Should().Be(originalIsAtBol);
            my.ConsoleState.LastTaskId.Should().Be(originalLastTaskId);
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
