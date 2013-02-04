//-----------------------------------------------------------------------
// <copyright file="IronTorso.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using IronPython.Runtime.Types;
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
            this.file = scriptFile;
            this.Engine = new IronEngine();
        }

        /// <summary>
        /// Retrieves the testing results, if it has been executed. If 'attribute'
        /// is defined, returns the value of that result attribute.
        /// </summary>
        /// <param name="attribute">The attribute name to lookup</param>
        /// <returns>The unittest framework's TestResults</returns>
        private dynamic GetResults(string attribute = "")
        {
            // check if tests have been run
            if (!this.HasRun || !this.Engine.Scope.ContainsVariable("result"))
            {
                return null;
            }

            var result = this.Engine.Scope.GetVariable("result");
            if (!string.IsNullOrEmpty(attribute) && ((IPythonObject)result).Dict.ContainsKey(attribute))
            {
                return ((IPythonObject)result).Dict[attribute];
            }

            // attribute not defined
            return result;
        }
        
        /// <summary>
        /// Gets the number of passed runs
        /// </summary>
        public int Passed
        {
            get
            {
                var result = this.GetResults("passed") ?? new List<object>();
                return result.Count;
            }
        }

        /// <summary>
        /// Gets the number of failed runs
        /// </summary>
        public int Failed
        {
            get
            {
                var result = this.GetResults("failures") ?? new List<object>();
                return result.Count;
            }
        }

        /// <summary>
        /// Gets the number of skipped runs
        /// </summary>
        public int Skipped
        {
            get
            {
                var result = this.GetResults("skipped") ?? new List<object>();
                return result.Count;
            }
        }

        /// <summary>
        /// Records whether the tests have been run or not
        /// </summary>
        public bool HasRun = false;

        /// <summary>
        /// Gets or sets the number of seconds to wait until a test is considered timed out
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
            try
            {
                Engine.Execute(this.file);
                var result = Engine.Scope.GetVariable("result");
                if (result == null)
                {
                    throw new Exception("Script did not return a valid result!");
                }

                HasRun = true;
            }
            catch (MissingMemberException)
            {
                Console.WriteLine("Script file needs to store results in 'result'!");
                throw;
            }
            catch (ThreadAbortException e)
            {
                // Ctrl-C was hit
                if (e.ExceptionState is Microsoft.Scripting.KeyboardInterruptException)
                {
                    Thread.ResetAbort();
                    Engine.Engine.Runtime.Shutdown();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception e)
            {
                // print internal Python stacktrace
                var op = Engine.Engine.GetService<ExceptionOperations>();
                Console.WriteLine(op.FormatException(e));
                throw;
            }
        }

        /// <summary>
        /// Dumps a text summary of the test cases' execution results into a file
        /// </summary>
        /// <param name="fileName">Output filename to write the report to</param>
        public void DumpReport(string fileName)
        {
            // we already have the results
            var logfile = this.GetResults("logfile");
            if (logfile == null)
            {
                Console.WriteLine("No results available.");
                return;
            }

            System.IO.File.Copy(logfile, fileName, true);
        }

        public void Dispose()
        {
            // nothing
        }
    }
}
