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
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Sharp.Async;

namespace PSConcurrent
{
    /// <summary>
    ///   Invokes <c>ScriptBlock</c>s concurrently.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "Concurrent")]
    [OutputType(typeof(TaskOutput))]
    public class InvokeConcurrentCmdlet : Cmdlet, IDisposable
    {
        private readonly List<Task>               _tasks;
        private readonly MainThreadDispatcher     _mainThread;
        private readonly ConsoleState             _console;
        private readonly ConcurrentBag<Exception> _exceptions;
        private readonly CancellationTokenSource  _cancellation;

        private TaskFactory _taskFactory;
        private int         _isDisposed;

        public InvokeConcurrentCmdlet()
        {
            _tasks        = new List<Task>();
            _mainThread   = new MainThreadDispatcher();
            _console      = new ConsoleState();
            _exceptions   = new ConcurrentBag<Exception>();
            _cancellation = new CancellationTokenSource();
        }

        [Parameter(
            Position                        = 0,
            Mandatory                       = true,
            ValueFromPipeline               = true,
            ValueFromPipelineByPropertyName = true
        )]
        [ValidateNotNull]
        [AllowEmptyCollection]
        public ScriptBlock[] ScriptBlock { get; set; }

        [Parameter()]
        [ValidateRange(1, int.MaxValue)]
        public int? MaxConcurrency { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true)]
        [AllowEmptyCollection]
        public PSVariable[] Variable { get; set; }

        [Parameter(ValueFromPipelineByPropertyName = true)]
        [AllowEmptyCollection]
        public PSModuleInfo[] Module { get; set; }

        private PSHost Host
            => CommandRuntime.Host;

        private PSHostUserInterface UI
            => CommandRuntime.Host.UI;

        protected override void BeginProcessing()
        {
            // Ask default scheduler to preserve FIFO ordering, so that tasks
            // tend to execute in that order, regardless of whether this cmdlet
            // uses LimitedConcurrencyTaskScheduler or the default scheduler.
            const TaskCreationOptions Creation
                = TaskCreationOptions.PreferFairness;

            // This cmdlet ensures only that the given ScriptBlocks execute
            // with the given maximum concurrency; if those ScriptBlocks invoke
            // further concurrent operations, this cmdlet does not limit those
            // operations' concurrency.
            const TaskContinuationOptions Continuation
                = TaskContinuationOptions.HideScheduler;

            // Choose limited-concurrency scheduler if the user specified a
            // maximum concurrency; otherwise use the default scheduler.
            var scheduler = MaxConcurrency.HasValue
                ? new LimitedConcurrencyTaskScheduler(MaxConcurrency.Value)
                : TaskScheduler.Default;

            // Encapsulate the above decisions into a task factory.
            _taskFactory = new TaskFactory(
                _cancellation.Token, Creation, Continuation, scheduler
            );
        }

        protected override void ProcessRecord()
        {
            foreach (var script in ScriptBlock)
            {
                if (script == null)
                    continue;

                if (_cancellation.IsCancellationRequested)
                    return;

                var taskId = _tasks.Count + 1;
                var work   = new TaskWork(script, Variable, Module);

                _tasks.Add(_taskFactory.StartNew(
                    () => TaskMain(taskId, work)
                ));
            }
        }

        protected override void EndProcessing()
        {
            Task.WhenAll(_tasks)
                .ContinueWith(_ => _mainThread.Complete());

            _mainThread.Run();

            ThrowCollectedExceptions();
        }

        protected override void StopProcessing()
        {
            // Invoked when a running command needs to be stopped, such as when
            // the user presses CTRL-C.  Invoked on a different thread than the
            // Begin/Process/End sequence.

            UI.WriteWarningLine("Canceling...");
            _cancellation.Cancel();
        }

        private void TaskMain(int taskId, TaskWork work)
        {
            var host = new TaskHost(Host, _console, taskId);
            try
            {
                host.UI.WriteLine("Starting");
                RunScript(taskId, work, host);
            }
            catch (Exception e)
            {
                _cancellation.Cancel();
                HandleException(e, taskId, host);
            }
            finally
            {
                host.UI.WriteLine("Ended");
            }
        }

        private void RunScript(int taskId, TaskWork work, TaskHost host)
        {
            var state = CreateInitialSessionState(taskId, work);

            using (var runspace = RunspaceFactory.CreateRunspace(host, state))
            {
                runspace.Name          = $"Invoke-Concurrent-{taskId}";
                runspace.ThreadOptions = PSThreadOptions.UseCurrentThread;
                runspace.Open();

                using (var shell = PowerShell.Create())
                {
                    var output = new PSDataCollection<PSObject>();
                    output.DataAdding += (s, e) => HandleOutput(taskId, e.ItemAdded);

                    shell.Runspace = runspace;
                    shell.AddScript(work.Script.ToString()).Invoke(null, output);
                }

                runspace.Close();
            }
        }

        private InitialSessionState CreateInitialSessionState(int taskId, TaskWork work)
        {
            var state = InitialSessionState.CreateDefault();

            foreach (var module in work.Modules)
                state.ImportPSModule(new[] { module.Path });

            foreach (var variable in work.Variables)
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
                "TaskId", taskId, null
            ));

            state.Variables.Add(new SessionStateVariableEntry(
                "ErrorActionPreference", "Stop", null
            ));

            return state;
        }

        private void HandleOutput(int taskId, object obj)
        {
            var output = new TaskOutput
            {
                TaskId = taskId,
                Object = (PSObject) obj
            };

            _mainThread.InvokeOnMainThread(() => WriteOutput(output));
        }
        
        protected virtual void WriteOutput(TaskOutput output)
        {
            // This method makes WriteObject virtual to accommodate testing.
            WriteObject(output);
        }

        private void HandleException(Exception e, int taskId, TaskHost host = null)
        {
            if (e is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    HandleException(inner, taskId, host);
            }
            else if (e.InnerException is Exception inner)
            {
                HandleException(inner, taskId, host);
            }
            else
            {
                _exceptions.Add(e);

                host.UI.WriteErrorLine(GetMostHelpfulMessage(e));

                var data = e.Data;
                if (data != null)
                    data["TaskId"] = taskId;
            }
        }

        private void ThrowCollectedExceptions()
        {
            var count = _exceptions.Count;
            if (count == 0)
                return;

            if (count == 1)
                ExceptionDispatchInfo.Capture(_exceptions.First()).Throw();

            throw new AggregateException(_exceptions);
        }

        private static string GetMostHelpfulMessage(Exception e)
            => (e as RuntimeException)?.ErrorRecord?.ErrorDetails?.Message
            ?? (e as RuntimeException)?.ErrorRecord?.Exception   ?.Message
            ?? e.Message;

        /// <summary>
        ///   Disposes unmanaged resources owned by the object.
        /// </summary>
        ~InvokeConcurrentCmdlet()
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
                _cancellation.Dispose();

            return disposed;
        }

        // Interlocked.Exchange doesn't support bool, so fake it with int
        private const int
            Yes = -1,
            No  =  0;
    }
}
