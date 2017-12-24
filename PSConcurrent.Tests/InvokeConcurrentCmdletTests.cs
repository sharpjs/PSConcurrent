/*
    Copyright (C) 2017 Jeffrey Sharp

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
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using FluentAssertions;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    [TestFixture]
    public class InvokeConcurrentCmdletTests
    {
        private InitialSessionState  InitialState;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            InitialState = InitialSessionState.CreateDefault();

            InitialState.Variables.Add(new SessionStateVariableEntry(
                "ErrorActionPreference", "Stop", null
            ));

            var modulePath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "PSConcurrent.psd1"
            );

            InitialState.ImportPSModule(new[] { modulePath });
        }

        [Test]
        public void OneScriptWithOutput()
        {
            var output = Run(@"
                Invoke-Concurrent { 42 }
            ");

            output.Raw.Should().HaveCount(1);

            output.OfWorker(1).Should().Contain(42);
        }

        [Test]
        public void MultiScriptWithOutput()
        {
            var output = Run(@"
                Invoke-Concurrent { 42 }, { 123 }, { 31337 }
            ");

            output.Raw.Should().HaveCount(3);

            output.OfWorker(1).Should().Contain(   42);
            output.OfWorker(2).Should().Contain(  123);
            output.OfWorker(3).Should().Contain(31337);
        }

        private Output Run(string script)
        {
            using (var shell = PowerShell.Create(InitialState))
                return new Output { Raw = shell.AddScript(script).Invoke() };
        }

        private class Output
        {
            public ICollection<PSObject> Raw;

            public IEnumerable<object> OfWorker(int id)
                => Raw
                    .Select(o => o.BaseObject)
                    .OfType<WorkerOutput>()
                    .Where(o => o.WorkerId == id)
                    .Select(o => o.Object);
        }
    }
}
