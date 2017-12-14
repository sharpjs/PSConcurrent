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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace PSParallel
{
    [Cmdlet(VerbsLifecycle.Invoke, "Parallel")]
    [OutputType(typeof(WorkerOutput))]
    public class InvokeParallelCmdlet : Cmdlet
    {
        private readonly CancellationTokenSource
            _cancellation = new CancellationTokenSource();

        private SemaphoreSlim                    _semaphore;
        private ConcurrentQueue<Task>            _workers;
        private int                              _workerCount;
        private MainThreadSynchronizationContext _context;
        private ConsoleState                     _console;
        private int                              _isDisposed;

        private PSHost Host
            => CommandRuntime.Host;

        private PSHostUserInterface UI
            => CommandRuntime.Host.UI;

        [Parameter(
            Position                        = 0,
            Mandatory                       = true,
            ValueFromPipeline               = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage
                = "The script block(s) to run in parallel."
        )]
        public ScriptBlock[] ScriptBlock { get; set; }

        [Parameter(
            HelpMessage
                = "The maximum number of simultaneously running script blocks.  "
                + "The default is the number of processor threads on the current computer."
        )]
        [ValidateRange(1, int.MaxValue)]
        public int? MaxConcurrency { get; set; }

        [Parameter(
            HelpMessage
                = "Variables to set for script blocks.  "
                + "Use Get-Variable to obtain PSVariable objects."
        )]
        public PSVariable[] Variable { get; set; }

        [Parameter(
            HelpMessage
                = "Modules to import for script blocks.  "
                + "Use Get-Module to obtain PSModuleInfo objects."
        )]
        public PSModuleInfo[] Module { get; set; }

        protected override void BeginProcessing()
        {
            var concurrency = MaxConcurrency ?? Environment.ProcessorCount;

            _semaphore = new SemaphoreSlim(
                initialCount: concurrency,
                maxCount:     concurrency
            );

            _workers = new ConcurrentQueue<Task>();
            _context = new MainThreadSynchronizationContext();
            _console = new ConsoleState();
        }

        protected override void ProcessRecord()
        {
            foreach (var script in ScriptBlock)
            {
                if (_cancellation.IsCancellationRequested)
                    return;

                _semaphore.Wait();

                var workerId = ++_workerCount;

                _workers.Enqueue(Task.Run(
                    () => WorkerMain(script, workerId),
                    _cancellation.Token
                ));
            }
        }

        protected override void EndProcessing()
        {
            var task = Task
                .WhenAll(_workers)
                .ContinueWith(_ => _context.Complete());

            _context.RunMainThread();
            _workers = null; // disposal unnecessary

            // Surface exception(s) from the tasks
            task.GetAwaiter().GetResult();
        }

        protected override void StopProcessing()
        {
            // Invoked when a running command needs to be stopped, such as when
            // the user presses CTRL-C.  Invoked on a different thread than the
            // Begin/Process/End sequence.

            UI.WriteWarningLine("Canceling...");
            _cancellation.Cancel();
        }

        private void WorkerMain(ScriptBlock script, int workerId)
        {
            var host = new WorkerHost(Host, _console, workerId);
            var oldContext = SynchronizationContext.Current;

            try
            {
                host.UI.WriteLine("Starting");
                SynchronizationContext.SetSynchronizationContext(_context);

                var state = CreateInitialSessionState();

                using (var runspace = RunspaceFactory.CreateRunspace(host, state))
                {
                    runspace.Name          = $"Invoke-Parallel-{workerId}";
                    runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
                    runspace.Open();

                    using (var shell = PowerShell.Create())
                    {
                        var output = new PSDataCollection<PSObject>();
                        output.DataAdding += (s, e) => OnOutput(workerId, e.ItemAdded);

                        shell.Runspace = runspace;
                        shell.AddScript(script.ToString()).Invoke(null, output);
                    }

                    runspace.Close();
                }
            }
            catch (Exception e)
            {
                host.UI.WriteErrorLine(e.Message);
                throw;
            }
            finally
            {
                host.UI.WriteLine("Ended");
                SynchronizationContext.SetSynchronizationContext(oldContext);
                _semaphore.Release();
            }
        }

        private void OnOutput(int workerId, object obj)
        {
            _context.Send(WriteObject, new WorkerOutput
            {
                WorkerId = workerId,
                Object   = obj
            });
        }

        private InitialSessionState CreateInitialSessionState()
        {
            var state = InitialSessionState.CreateDefault();

            if (Module != null)
                foreach (var module in Module)
                    state.ImportPSModule(new[] { module.Path });

            if (Variable != null)
                foreach (var variable in Variable)
                    state.Variables.Add(new SessionStateVariableEntry(
                        variable.Name,
                        variable.Value,
                        variable.Description,
                        variable.Options,
                        variable.Attributes
                    ));

            state.Variables.Add(new SessionStateVariableEntry(
                "CancellationToken", _cancellation.Token, null
            ));

            state.Variables.Add(new SessionStateVariableEntry(
                "ErrorActionPreference", "Stop", null
            ));

            return state;
        }

        /// <summary>
        ///   Disposes unmanaged resources owned by the object.
        /// </summary>
        ~InvokeParallelCmdlet()
        {
            Dispose(managed: false);
        }

        /// <summary>
        ///   Disposes managed and unmanaged resources owned by the object.
        /// </summary>
        public void Dispose()
        {
            Dispose(managed: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///   Disposes resources owned by the object.
        /// </summary>
        /// <param name="managed">
        ///   Whether to dispose managed resources
        ///   (in addition to unmanaged resources, which are always disposed).
        /// </param>
        /// <returns>
        ///   <c>true</c> if the object transitioned from not-disposed to disposed,
        ///   <c>false</c> if the object was disposed already.
        /// </returns>
        protected virtual bool Dispose(bool managed = true)
        {
            var disposed = Interlocked.Exchange(ref _isDisposed, Yes) == No;

            if (disposed && managed)
            {
                if (_workers != null)
                {
                    while (_workers.TryDequeue(out var task))
                        task.Dispose();
                    _workers = null;
                }

                if (_semaphore != null)
                {
                    _semaphore.Dispose();
                    _semaphore   = null;
                }

                _cancellation.Dispose();
            }

            return disposed;
        }

        // Interlocked.Exchange doesn't support bool, so fake it with int
        private const int
            Yes = -1,
            No  =  0;
    }
}
