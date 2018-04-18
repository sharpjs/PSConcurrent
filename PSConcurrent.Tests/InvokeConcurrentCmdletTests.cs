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
using System.Linq;
using System.Management.Automation;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Extensions;
using Moq;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class InvokeConcurrentCmdletTests : CmdletTests
    {
        [Test]
        public void One()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}"
            );

            output.Should().HaveCount(1);

            output.OfTask(1).Should().Contain("a");
        }

        [Test]
        public void Multiple()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}, {'b'}, {'c'}"
            );

            output.Should().HaveCount(3);

            output.OfTask(1).Should().Contain("a");
            output.OfTask(2).Should().Contain("b");
            output.OfTask(3).Should().Contain("c");
        }

        [Test]
        public void Multiple_LimitedConcurrency()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}, {'b'}, {'c'} -MaxConcurrency 2"
            );

            output.Should().HaveCount(3);

            output.OfTask(1).Should().Contain("a");
            output.OfTask(2).Should().Contain("b");
            output.OfTask(3).Should().Contain("c");
        }

        [Test]
        public void Multiple_LimitedConcurrency_Throwing()
        {
            var output = Invoke(
                @"Invoke-Concurrent {'a'}, {throw 'b'}, {'c'} -MaxConcurrency 1",
                a => a.Should().Throw<CmdletInvocationException>()
            );

            output.Should().HaveCount(1);

            output.OfTask(1).Should().Contain("a");
        }

        [Test]
        public void Variables()
        {
            var output = Invoke(
                @"
                    $Foo = 'Foo'
                    $Bar = 'Bar'
                    Invoke-Concurrent `
                        { $Foo = 'Wrong Foo'; $Bar = 'Wrong Bar' },
                        { Start-Sleep -Milliseconds 50; $Foo },
                        { Start-Sleep -Milliseconds 50; $Bar } `
                        -Variable (Get-Variable Foo), (Get-Variable Bar)
                "
            );

            output.Should().HaveCount(2);

            output.OfTask(1).Should().BeEmpty();
            output.OfTask(2).Should().Contain("Foo");
            output.OfTask(3).Should().Contain("Bar");
        }

        [Test]
        public void Modules()
        {
            var output = Invoke(
                $@"
                    cd ""{TestContext.CurrentContext.TestDirectory}""
                    Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                    Invoke-Concurrent `
                        {{ Invoke-TestModuleA }},
                        {{ Invoke-TestModuleB }} `
                        -Module (Get-Module TestModuleA), (Get-Module TestModuleB)
                "
            );

            output.Should().HaveCount(2);

            output.OfTask(1).Should().Contain("TestModuleA Output");
            output.OfTask(2).Should().Contain("TestModuleB Output");
        }

        [Test]
        public void TaskIdVariable()
        {
            var output = Invoke(
                @"Invoke-Concurrent {$TaskId}, {$TaskId}, {$TaskId}"
            );

            output.Should().HaveCount(3);

            output.OfTask(1).Should().Contain(1);
            output.OfTask(2).Should().Contain(2);
            output.OfTask(3).Should().Contain(3);
        }

        [Test]
        public void ErrorActionPreferenceVariable()
        {
            var output = Invoke(
                @"Invoke-Concurrent {$ErrorActionPreference}"
            );

            output.Should().HaveCount(1);
            output.OfTask(1).Single().Should().Be("Stop");
        }

        [Test]
        public void CancellationTokenVariable()
        {
            var output = Invoke(
                @"Invoke-Concurrent {$CancellationToken}"
            );

            output.Should().HaveCount(1);
            output.OfTask(1).Single()
                .Should().BeAssignableTo<CancellationToken>()
                .And.NotBe(default(CancellationToken));
        }

        [Test]
        public void StopBeforeFirstRecord()
        {
            var cmdlet = new InvokeConcurrentCmdletHarness();

            Mock.Get(cmdlet.CommandRuntime)
                .Setup(r => r.Host.UI.WriteWarningLine("Canceling..."))
                .Verifiable();

            cmdlet.BeginProcessing();

            cmdlet.StopProcessing();

            cmdlet.ScriptBlock = new[] { ScriptBlock.Create("'a'") };
            cmdlet.ProcessRecord();

            cmdlet.EndProcessing();

            cmdlet.Output.Should().BeEmpty();

            Mock.Get(cmdlet.CommandRuntime).Verify();
        }

        [Test]
        public void StopBetweenRecords()
        {
            var cmdlet = new InvokeConcurrentCmdletHarness();

            Mock.Get(cmdlet.CommandRuntime)
                .Setup(r => r.Host.UI.WriteWarningLine("Canceling..."))
                .Verifiable();

            cmdlet.BeginProcessing();

            cmdlet.ScriptBlock = new[] { ScriptBlock.Create("'a'") };
            cmdlet.ProcessRecord();

            Thread.Sleep(1.Seconds()); // allow task to proceed

            cmdlet.StopProcessing();

            cmdlet.ScriptBlock = new[] { ScriptBlock.Create("'b'") };
            cmdlet.ProcessRecord();

            cmdlet.EndProcessing();

            cmdlet.Output.Should().HaveCount(1);
            cmdlet.Output.OfTask(1).Should().Contain("a");

            Mock.Get(cmdlet.CommandRuntime).Verify();
        }

        [Test]
        public void StopAfterLastRecord()
        {
            var cmdlet = new InvokeConcurrentCmdletHarness();

            Mock.Get(cmdlet.CommandRuntime)
                .Setup(r => r.Host.UI.WriteWarningLine("Canceling..."))
                .Verifiable();

            cmdlet.BeginProcessing();

            cmdlet.ScriptBlock = new[] { ScriptBlock.Create("'a'") };
            cmdlet.ProcessRecord();
            Thread.Sleep(1.Seconds()); // allow task to proceed

            cmdlet.ScriptBlock = new[] { ScriptBlock.Create("'b'") };
            cmdlet.ProcessRecord();
            Thread.Sleep(1.Seconds()); // allow task to proceed

            cmdlet.StopProcessing();

            cmdlet.EndProcessing();

            cmdlet.Output.Should().HaveCount(2);
            cmdlet.Output.OfTask(1).Should().Contain("a");
            cmdlet.Output.OfTask(2).Should().Contain("b");

            Mock.Get(cmdlet.CommandRuntime).Verify();
        }

        private class InvokeConcurrentCmdletHarness : InvokeConcurrentCmdlet
        {
            public InvokeConcurrentCmdletHarness()
            {
                CommandRuntime = Mock.Of<ICommandRuntime>();
                Output         = new ConcurrentQueue<TaskOutput>();
            }

            public ConcurrentQueue<TaskOutput> Output { get; }

            public new void BeginProcessing() => base.BeginProcessing();
            public new void ProcessRecord()   => base.ProcessRecord();
            public new void EndProcessing()   => base.EndProcessing();
            public new void StopProcessing()  => base.StopProcessing();

            protected override void WriteOutput(TaskOutput output)
                => Output.Enqueue(output);
        }
    }
}
