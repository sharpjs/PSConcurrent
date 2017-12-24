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

using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using NUnit.Framework;

namespace PSConcurrent.Tests
{
    public class CmdletTests
    {
        private InitialSessionState _initialState;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _initialState = InitialSessionState.CreateDefault();

            _initialState.Variables.Add(new SessionStateVariableEntry(
                "ErrorActionPreference", "Stop", null
            ));

            var modulePath = Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "PSConcurrent.psd1"
            );

            _initialState.ImportPSModule(new[] { modulePath });
        }

        protected ICollection<PSObject> Invoke(string script)
        {
            using (var shell = PowerShell.Create(_initialState))
                return shell.AddScript(script).Invoke();
        }
    }
}
