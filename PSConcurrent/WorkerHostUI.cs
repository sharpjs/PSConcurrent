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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;

namespace PSConcurrent
{
    internal class WorkerHostUI : PSHostUserInterface
    {
        private readonly PSHostUserInterface _ui;        // Underlying UI implementation
        private readonly ConsoleState        _console;   // Overall console state
        private readonly int                 _workerId;  // Identifier of this worker
        private          bool                _workerBol; // Whether this worker should be at BOL

        internal WorkerHostUI(PSHostUserInterface ui, ConsoleState console, int workerId)
        {
            _ui        = ui      ?? throw new ArgumentNullException(nameof(ui));
            _console   = console ?? throw new ArgumentNullException(nameof(console));
            _workerId  = workerId;
            _workerBol = true;

            Header = $"Worker {workerId}";
        }

        public string Header { get; set; }

        public override PSHostRawUserInterface RawUI
            => _ui.RawUI;

        public override bool SupportsVirtualTerminal
            => _ui.SupportsVirtualTerminal;

        public override void Write(string text)
        {
            lock (_console)
            {
                _ui.Write(Prepare(text));
                Update(EndsWithEol(text));
            }
        }

        public override void Write(ConsoleColor foreground, ConsoleColor background, string text)
        {
            lock (_console)
            {
                _ui.Write(foreground, background, Prepare(text));
                Update(EndsWithEol(text));
            }
        }

        public override void WriteLine()
        {
            WriteLine("");
        }

        public override void WriteLine(string text)
        {
            lock (_console)
            {
                _ui.WriteLine(Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteLine(ConsoleColor foreground, ConsoleColor background, string text)
        {
            lock (_console)
            {
                _ui.WriteLine(foreground, background, Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteDebugLine(string text)
        {
            lock (_console)
            {
                _ui.WriteDebugLine(Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteVerboseLine(string text)
        {
            lock (_console)
            {
                _ui.WriteVerboseLine(Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteWarningLine(string text)
        {
            lock (_console)
            {
                _ui.WriteWarningLine(Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteErrorLine(string text)
        {
            lock (_console)
            {
                _ui.WriteErrorLine(Prepare(text));
                Update(eol: true);
            }
        }

        public override void WriteInformation(InformationRecord record)
        {
            lock (_console)
            {
                // NOTE: Do not modify record; doing so results in duplicate prefixes
                _ui.WriteInformation(record);
            }
        }

        public override void WriteProgress(long sourceId, ProgressRecord record)
        {
            lock (_console)
            {
                // NOTE: Do not modify record; it is not presented in the textual log
                _ui.WriteProgress(sourceId, record);
            }
        }

        public override string ReadLine()
        {
            lock (_console)
            {
                var result = _ui.ReadLine();
                Update(eol: true);
                return result;
            }
        }

        public override SecureString ReadLineAsSecureString()
        {
            lock (_console)
            {
                var result = _ui.ReadLineAsSecureString();
                Update(eol: true);
                return result;
            }
        }

        public override Dictionary<string, PSObject> Prompt(
            string caption, string message, Collection<FieldDescription> descriptions)
        {
            lock (_console)
                return _ui.Prompt(caption, message, descriptions);
        }

        public override int PromptForChoice(
            string caption, string message, Collection<ChoiceDescription> choices, int defaultChoice)
        {
            lock (_console)
                return _ui.PromptForChoice(caption, message, choices, defaultChoice);
        }

        public override PSCredential PromptForCredential(
            string caption, string message, string userName, string targetName)
        {
            lock (_console)
                return _ui.PromptForCredential(caption, message, userName, targetName);
        }

        public override PSCredential PromptForCredential(
            string caption, string message, string userName, string targetName,
            PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options)
        {
            lock (_console)
                return _ui.PromptForCredential(
                    caption, message, userName, targetName, allowedCredentialTypes, options);
        }

        private string Prepare(string text)
        {
            if (!_console.IsAtBol)
            {
                if (_console.LastWorkerId == _workerId)
                    // The console is not at BOL, but this worker was the last
                    // one to write to it.  The console state is as the worker
                    // expects.  There is no need to modify the console state or
                    // to add any information to the text.
                    return text;

                // The console is not at BOL, because some other worker wrote
                // a partial line to it.  End that line, so that this worker's
                // text will start on a new line.
                _ui.WriteLine();
                _console.IsAtBol = true;
            }

            // The console is at BOL.  Prefix the text with the line header.
            // If this worker last wrote a partial line that was interrupted by
            // some other worker, add a line continuation indicator.
            return _workerBol
                ? $"[{Header}]: {text}"
                : $"[{Header}]: (...) {text}";
        }

        private void Update(bool eol)
        {
            _console.IsAtBol      = _workerBol = eol;
            _console.LastWorkerId = _workerId;
        }

        private static bool EndsWithEol(string value)
            => value != null
            && value.EndsWith("\n", StringComparison.Ordinal);
    }
}
