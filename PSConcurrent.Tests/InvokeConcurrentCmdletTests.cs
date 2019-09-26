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
using System.Collections.Concurrent;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Extensions;
using Moq;
using NUnit.Framework;
using static PSConcurrent.TestEnvironment;

namespace PSConcurrent
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class InvokeConcurrentCmdletTests
    {
        [Test]
        public void One_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {'a'}
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("a");

            e.Should().BeNull();
        }

        [Test]
        public void One_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                {'a'} | Invoke-Concurrent
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("a");

            e.Should().BeNull();
        }

        [Test]
        public void One_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                [PSCustomObject] @{ ScriptBlock = {'a'} } | Invoke-Concurrent
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("a");

            e.Should().BeNull();
        }

        [Test]
        public void Multiple_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {'a'}, {'b'}, {'c'}
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void Multiple_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                {'a'}, {'b'}, {'c'} | Invoke-Concurrent
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void Multiple_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                [PSCustomObject] @{ ScriptBlock = {'a'} },
                [PSCustomObject] @{ ScriptBlock = {'b'} },
                [PSCustomObject] @{ ScriptBlock = {'c'} } `
                | Invoke-Concurrent
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void LimitedConcurrency_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {'a'}, {'b'}, {'c'} -MaxConcurrency 2
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void LimitedConcurrency_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                {'a'}, {'b'}, {'c'} | Invoke-Concurrent -MaxConcurrency 2
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void LimitedConcurrency_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                [PSCustomObject] @{ ScriptBlock = {'a'} },
                [PSCustomObject] @{ ScriptBlock = {'b'} },
                [PSCustomObject] @{ ScriptBlock = {'c'} } `
                | Invoke-Concurrent -MaxConcurrency 2
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal("a");
            output.OfTask(2).Should().Equal("b");
            output.OfTask(3).Should().Equal("c");

            e.Should().BeNull();
        }

        [Test]
        public void LimitedConcurrency_Throwing()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {'a'}, {throw 'b'}, {'c'} -MaxConcurrency 1
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("a");

            e.Should().NotBeNull().And.BeAssignableTo<RuntimeException>();
        }

        [Test]
        public void AmbientVariable_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                Invoke-Concurrent {$Foo}
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal(null as object);

            e.Should().BeNull();
        }

        [Test]
        public void AmbientVariable_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                {$Foo} | Invoke-Concurrent
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal(null as object);

            e.Should().BeNull();
        }

        [Test]
        public void AmbientVariable_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                [PSCustomObject] @{ ScriptBlock = {$Foo} } | Invoke-Concurrent
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal(null as object);

            e.Should().BeNull();
        }

        [Test]
        public void VariableArgument_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'; $Bar = 'Bar'
                Invoke-Concurrent {$Foo; $Bar} -Variable (gv Foo), (gv Bar)
            ");

            output.Should().HaveCount(2);
            output.OfTask(1).Should().Equal("Foo", "Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableArgument_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'; $Bar = 'Bar'
                {$Foo; $Bar} | Invoke-Concurrent -Variable (gv Foo), (gv Bar)
            ");

            output.Should().HaveCount(2);
            output.OfTask(1).Should().Equal("Foo", "Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableArgument_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'; $Bar = 'Bar'
                [PSCustomObject] @{
                    ScriptBlock = {$Foo; $Bar}
                },
                [PSCustomObject] @{
                    ScriptBlock = {$Foo}, {$Bar}
                } |
                Invoke-Concurrent -Variable (gv Foo), (gv Bar)
            ");

            output.Should().HaveCount(4);
            output.OfTask(1).Should().Equal("Foo", "Bar");
            output.OfTask(2).Should().Equal("Foo");
            output.OfTask(3).Should().Equal("Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableProperty_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'; $Bar = 'Bar'
                [PSCustomObject] @{
                    ScriptBlock = {$Foo; $Bar}
                    Variable    = (gv Foo), (gv Bar)
                },
                [PSCustomObject] @{
                    ScriptBlock = {$Foo}, {$Bar}
                    Variable    = (gv Foo), (gv Bar)
                } |
                Invoke-Concurrent
            ");

            output.Should().HaveCount(4);
            output.OfTask(1).Should().Equal("Foo", "Bar");
            output.OfTask(2).Should().Equal("Foo");
            output.OfTask(3).Should().Equal("Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableArgumentAndProperty_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'; $Bar = 'Bar'
                [PSCustomObject] @{
                    ScriptBlock = {$Foo; $Bar}
                    Variable    = (gv Foo)
                } |
                Invoke-Concurrent -Variable (gv Bar)
            ");

            output.Should().HaveCount(2);
            output.OfTask(1).Should().Equal(null, "Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableIsolation_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                Invoke-Concurrent {$Foo = 'Wrong'}, {$Foo} `
                    -Variable (gv Foo) -MaxConcurrency 1
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().BeEmpty();
            output.OfTask(2).Should().Equal("Foo");

            e.Should().BeNull();
        }

        [Test]
        public void VariableIsolation_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                {$Foo = 'Bad'}, {$Foo} |
                Invoke-Concurrent -Variable (gv Foo) -MaxConcurrency 1
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().BeEmpty();
            output.OfTask(2).Should().Equal("Foo");

            e.Should().BeNull();
        }

        [Test]
        public void VariableIsolation_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Foo = 'Foo'
                [PSCustomObject] @{
                    ScriptBlock = {$Foo = 'Bad'}, {$Foo}
                    Variable    = (gv Foo)
                } |
                Invoke-Concurrent -MaxConcurrency 1
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().BeEmpty();
            output.OfTask(2).Should().Equal("Foo");

            e.Should().BeNull();
        }

        [Test]
        public void VariableReference_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                $Bag = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
                Invoke-Concurrent {$Bag.Add('Foo')}, {$Bag.Add('Bar')} -Variable (gv Bag)
                $Bag.ToArray()
            ");

            output.Should().HaveCount(2);
            output.Select(o => o.BaseObject).Should().Contain("Foo").And.Contain("Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableReference_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                $Bag = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
                {$Bag.Add('Foo')}, {$Bag.Add('Bar')} | Invoke-Concurrent -Variable (gv Bag)
                $Bag.ToArray()
            ");

            output.Should().HaveCount(2);
            output.Select(o => o.BaseObject).Should().Contain("Foo").And.Contain("Bar");

            e.Should().BeNull();
        }

        [Test]
        public void VariableReference_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                $Bag = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
                [PSCustomObject] @{
                    ScriptBlock = {$Bag.Add('Foo')}, {$Bag.Add('Bar')}
                    Variable    = (gv Bag)
                } `
                | Invoke-Concurrent
                $Bag.ToArray()
            ");

            output.Should().HaveCount(2);
            output.Select(o => o.BaseObject).Should().Contain("Foo").And.Contain("Bar");

            e.Should().BeNull();
        }

        [Test]
        public void AmbientModule_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1
                Invoke-Concurrent {gmo Test-ModuleA}
            ");

            output.Should().HaveCount(0);

            e.Should().BeNull();
        }

        [Test]
        public void AmbientModule_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1
                {gmo Test-ModuleA} | Invoke-Concurrent
            ");

            output.Should().HaveCount(0);

            e.Should().BeNull();
        }

        [Test]
        public void AmbientModule_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1
                [PSCustomObject] @{
                    ScriptBlock = {gmo Test-ModuleA}
                } |
                Invoke-Concurrent
            ");

            output.Should().HaveCount(0);

            e.Should().BeNull();
        }

        [Test]
        public void ModuleArgument_ArgumentBlock()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                Invoke-Concurrent {Invoke-TestModuleA; Invoke-TestModuleB} `
                    -Module (gmo TestModuleA), (gmo TestModuleB)
            ");

            output.Should().HaveCount(2);
            output.OfTask(1).Should().Equal("TestModuleA Output", "TestModuleB Output");

            e.Should().BeNull();
        }

        [Test]
        public void ModuleArgument_PipelinedBlock()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                {Invoke-TestModuleA; Invoke-TestModuleB} `
                    | Invoke-Concurrent -Module (gmo TestModuleA), (gmo TestModuleB)
            ");

            output.Should().HaveCount(2);
            output.OfTask(1).Should().Equal("TestModuleA Output", "TestModuleB Output");

            e.Should().BeNull();
        }

        [Test]
        public void ModuleArgument_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                [PSCustomObject] @{
                    ScriptBlock = {Invoke-TestModuleA; Invoke-TestModuleB}
                },
                [PSCustomObject] @{
                    ScriptBlock = {Invoke-TestModuleA}, {Invoke-TestModuleB}
                } |
                Invoke-Concurrent -Module (gmo TestModuleA), (gmo TestModuleB)
            ");

            output.Should().HaveCount(4);
            output.OfTask(1).Should().Equal("TestModuleA Output", "TestModuleB Output");
            output.OfTask(2).Should().Equal("TestModuleA Output");
            output.OfTask(3).Should().Equal("TestModuleB Output");

            e.Should().BeNull();
        }

        [Test]
        public void ModuleProperty_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                [PSCustomObject] @{
                    ScriptBlock = {Invoke-TestModuleA; Invoke-TestModuleB}
                    Module      = (gmo TestModuleA), (gmo TestModuleB)
                },
                [PSCustomObject] @{
                    ScriptBlock = {Invoke-TestModuleA}, {Invoke-TestModuleB}
                    Module      = (gmo TestModuleA), (gmo TestModuleB)
                } |
                Invoke-Concurrent
            ");

            output.Should().HaveCount(4);
            output.OfTask(1).Should().Equal("TestModuleA Output", "TestModuleB Output");
            output.OfTask(2).Should().Equal("TestModuleA Output");
            output.OfTask(3).Should().Equal("TestModuleB Output");

            e.Should().BeNull();
        }

        [Test]
        public void ModuleArgumentAndProperty_PipelinedObject()
        {
            var (output, e) = Invoke(@"
                Import-Module .\TestModuleA.psm1, .\TestModuleB.psm1
                [PSCustomObject] @{
                    ScriptBlock = {gmo TestModuleA; Invoke-TestModuleB}
                    Module      = (gmo TestModuleA)
                } |
                Invoke-Concurrent -Module (gmo TestModuleB)
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("TestModuleB Output");

            e.Should().BeNull();
        }

        [Test]
        public void TaskIdVariable()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {$TaskId}, {$TaskId}, {$TaskId}
            ");

            output.Should().HaveCount(3);
            output.OfTask(1).Should().Equal(1);
            output.OfTask(2).Should().Equal(2);
            output.OfTask(3).Should().Equal(3);

            e.Should().BeNull();
        }

        [Test]
        public void ErrorActionPreferenceVariable()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {$ErrorActionPreference}
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Should().Equal("Stop");

            e.Should().BeNull();
        }

        [Test]
        public void CancellationTokenVariable()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {$CancellationToken}
            ");

            output.Should().HaveCount(1);
            output.OfTask(1).Single()
                .Should().BeAssignableTo<CancellationToken>()
                .And.NotBe(default(CancellationToken));

            e.Should().BeNull();
        }

        [Test]
        public void SingleException()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent { throw [InvalidOperationException] 'Oops' }
            ");

            output.Should().BeEmpty();

            e               .Should().BeAssignableTo<RuntimeException>();
            e.InnerException.Should().BeAssignableTo<InvalidOperationException>();
        }

        [Test]
        public void MultiException()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent `
                    { sleep -m 100; throw [InvalidOperationException] 'Oops A' },
                    { sleep -m 100; throw [InvalidOperationException] 'Oops B' }
            ");

            output.Should().BeEmpty();

            e               .Should().BeAssignableTo<RuntimeException>();
            e.InnerException.Should().BeAssignableTo<AggregateException>();

            ((AggregateException) e.InnerException).InnerExceptions
                .Should().HaveCount(2)
                .And.AllBeAssignableTo<InvalidOperationException>();
        }

        [Test]
        public void NestedSingleException()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {
                    throw [AggregateException]::new(
                        [InvalidOperationException] 'Oops A'
                    )
                }
            ");

            output.Should().BeEmpty();

            e               .Should().BeAssignableTo<RuntimeException>();
            e.InnerException.Should().BeAssignableTo<InvalidOperationException>();
        }

        [Test]
        public void NestedMultiException()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {
                    throw [AggregateException]::new(
                        [InvalidOperationException] 'Oops A',
                        [InvalidOperationException] 'Oops B'
                    )
                }
            ");

            output.Should().BeEmpty();

            e               .Should().BeAssignableTo<RuntimeException>();
            e.InnerException.Should().BeAssignableTo<AggregateException>();

            ((AggregateException) e.InnerException).InnerExceptions
                .Should().HaveCount(2)
                .And.AllBeAssignableTo<InvalidOperationException>();
        }

        [Test]
        public void NestedJaggedException()
        {
            var (output, e) = Invoke(@"
                Invoke-Concurrent {
                    throw [AggregateException]::new(
                        [InvalidOperationException] 'Oops A',
                        [AggregateException]::new(
                            [InvalidOperationException] 'Oops B'
                        )
                    )
                }
            ");

            output.Should().BeEmpty();

            e               .Should().BeAssignableTo<RuntimeException>();
            e.InnerException.Should().BeAssignableTo<AggregateException>();

            ((AggregateException) e.InnerException).InnerExceptions
                .Should().HaveCount(2)
                .And.AllBeAssignableTo<InvalidOperationException>();
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
