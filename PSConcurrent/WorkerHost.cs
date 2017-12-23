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
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;

namespace PSConcurrent
{
    internal class WorkerHost : PSHost
    {
        private readonly PSHost       _host;
        private readonly WorkerHostUI _ui;
        private readonly Guid         _id;
        private readonly string       _name;

        private static readonly Version _version
            = typeof(WorkerHost).Assembly.GetName().Version;

        internal WorkerHost(PSHost host, ConsoleState state, int workerId)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _ui   = new WorkerHostUI(host.UI, state, workerId);
            _id   = Guid.NewGuid();
            _name = $"Invoke-Concurrent[{workerId}]";
        }

        public override Guid InstanceId
            => _id;

        public override string Name
            => _name;

        public override Version Version
            => _version;

        public override PSHostUserInterface UI
            => _ui;

        public override CultureInfo CurrentCulture
            => _host.CurrentCulture;

        public override CultureInfo CurrentUICulture
            => _host.CurrentUICulture;

        public override PSObject PrivateData
            => _host.PrivateData;

        public override bool DebuggerEnabled
        {
            get => _host.DebuggerEnabled;
            set => _host.DebuggerEnabled = value;
        }

        public override void EnterNestedPrompt()
            => _host.EnterNestedPrompt();

        public override void ExitNestedPrompt()
            => _host.ExitNestedPrompt();

        public override void NotifyBeginApplication()
            => _host.NotifyBeginApplication();

        public override void NotifyEndApplication()
            => _host.NotifyEndApplication();

        public override void SetShouldExit(int exitCode)
            => _host.SetShouldExit(exitCode);
    }
}
