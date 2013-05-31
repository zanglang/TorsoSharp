//-----------------------------------------------------------------------
// <copyright file="IronTorso.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Scripting.Hosting;

    /// <summary>
    /// ITorso implementation that runs IronPython tests
    /// </summary>
    class IronTorso : ITorso
    {
        /// <summary>
        /// Initializes a new instance of the Torso class
        /// </summary>
        /// <param name="scriptFile">Path to an IronPython script file</param>
        public IronTorso(string scriptFile)
        {
            // default 2 hours
            this.Timeout = 7200;
            this.file = scriptFile;
            this.Engine = new IronEngine();
        }

        /// <summary>
        /// Gets the number of passed runs
        /// </summary>
        public int Passed
        {
            get
            {
                return Engine.Scope.ContainsVariable("passed") ? Engine.Scope.GetVariable<int>("passed") : 0;
            }
        }

        /// <summary>
        /// Gets the number of failed runs
        /// </summary>
        public int Failed
        {
            get
            {
                return Engine.Scope.ContainsVariable("failed") ? Engine.Scope.GetVariable<int>("failed") : 0;
            }
        }

        /// <summary>
        /// Gets the number of skipped runs
        /// </summary>
        public int Skipped
        {
            get
            {
                return Engine.Scope.ContainsVariable("skipped") ? Engine.Scope.GetVariable<int>("skipped") : 0;
            }
        }

        /// <summary>
        /// Records whether the tests have been run or not
        /// </summary>
        public bool HasRun = false;

        /// <summary>
        /// Gets or sets the number of seconds to wait until a test is considered timed out. Default: 2 hours.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// IronPython script engine
        /// </summary>
        public IronEngine Engine { get; private set; }

        /// <summary>
        /// Target IronPython script file to run
        /// </summary>
        private readonly string file;

        /// <summary>
        /// Runs all test case steps as defined in the run file
        /// </summary>
        public void RunAll()
        {
            // create a cancellation token for the task
            var source = new CancellationTokenSource();
            var t = Task.Factory.StartNew(
                () =>
                    {
                        Engine.Execute(this.file);
                        HasRun = true;
                    },
                source.Token);

            try
            {
                if (t.Wait(this.Timeout * 1000) == false)
                {
                    // run has timed out
                    source.Cancel();
                    throw new TimeoutException(@"
***********************************************
Timed out after " + this.Timeout + @" seconds, terminating!
***********************************************");
                }
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    // Ctrl-C was hit
                    if (e is ThreadAbortException
                        && ((ThreadAbortException)e).ExceptionState is Microsoft.Scripting.KeyboardInterruptException)
                    {
                        Thread.ResetAbort();
                        Engine.Engine.Runtime.Shutdown();
                        return;
                    }

                    // print internal Python stacktrace
                    var op = Engine.Engine.GetService<ExceptionOperations>();
                    Console.WriteLine(op.FormatException(e));
                    throw;
                }
            }
        }

        /// <summary>
        /// Dumps a text summary of the test cases' execution results into a file
        /// </summary>
        /// <param name="fileName">Output filename to write the report to</param>
        public void DumpReport(string fileName)
        {
            if (!this.Engine.Scope.ContainsVariable("logfile"))
            {
                Console.WriteLine("Script file did not return a 'logfile' variable.");
                return;
            }

            var logfile = this.Engine.Scope.GetVariable<string>("logfile");
            System.IO.File.Copy(logfile, fileName, true);
        }

        public void Dispose()
        {
            // nothing
        }
    }
}
