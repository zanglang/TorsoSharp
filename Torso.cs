//-----------------------------------------------------------------------
// <copyright file="Torso.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    /// <summary>
    /// Torso class
    /// </summary>
    public class Torso : ITorso
    {
        /// <summary>
        /// Root folder to find test stub instructions
        /// </summary>
        private const string ResourcesPath = @"y:\mufat_resources\sdkruntime";

        /// <summary>
        /// <code>IntPtr</code> handle wrapper to muFAT proxy DLL
        /// </summary>
        private readonly SafeLibraryHandle handle;

        /// <summary>
        /// <code>IntPtr</code> handle to <code>CMVCore</code> object
        /// </summary>
        private readonly IntPtr baseObject;
        
        /// <summary>
        /// Initializes a new instance of the Torso class
        /// </summary>
        /// <param name="configFile">Path to run configuration file</param>
        /// <param name="proxy">Path to muFAT proxy DLL</param>
        /// <param name="timeout">How long to wait for a run to complete before forcefully terminating</param>
        public Torso(string configFile, string proxy = "muFATProxy.dll", int timeout = 7200)
        {
            if (!File.Exists(configFile))
            {
                throw new IOException("Run file " + configFile + " does not exist");
            }
            
            if (!File.Exists(proxy))
            {
                throw new IOException("Proxy " + proxy + " does not exist");
            }

            this.Passed = this.Failed = 0;
            this.Steps = new List<Step>();
            this.StartTime = DateTime.Now;
            this.Timeout = timeout;

            // load the muFAT proxy DLL
            this.handle = NativeMethods.LoadLibrary(proxy);
            if (this.handle.IsInvalid)
            {
                int hr = Marshal.GetHRForLastWin32Error();
                Marshal.ThrowExceptionForHR(hr);
            }

            // initialize muFAT proxy
            if (!this.GetDelegate<NativeMethods.Init>("Init").Invoke())
            {
                throw new Exception("Could not initialize proxy");
            }

            // initializes CMVCore on the muvee Runtime
            this.baseObject = this.GetDelegate<NativeMethods.GetBaseObject>("GetBaseObject").Invoke();
            if (this.baseObject == IntPtr.Zero)
            {
                throw new Exception("Could not get base object");
            }


            // parse the run file
            this.Parse(configFile);
        }

        /// <summary>
        /// Gets the list of test case steps
        /// </summary>
        public List<Step> Steps { get; private set; }

        /// <summary>
        /// Gets the start time when the Torso was created
        /// </summary>
        public DateTime StartTime { get; private set; }

        /// <summary>
        /// Gets the number of passed runs
        /// </summary>
        public int Passed { get; private set; }

        /// <summary>
        /// Gets the number of failed runs
        /// </summary>
        public int Failed { get; private set; }

        public int Skipped
        {
            get
            {
                return this.Steps.Count - this.Passed - this.Failed;
            }
        }

        /// <summary>
        /// Gets or sets the number of seconds to wait until a test is considered timed out
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Append a string to the debugging log file
        /// </summary>
        /// <param name="log">String to write</param>
        /// <param name="args">Miscellaneous parameters for formatting</param>
        public static void Log(string log, params object[] args)
        {
            if (!Directory.Exists(@"C:\muveeDebug"))
            {
                return;
            }

            string logFile = @"C:\muveeDebug\LoggingError.txt";
            if (File.Exists(@"C:\muveeDebug\Log.txt"))
            {
                logFile = @"C:\muveeDebug\Log.txt";
            }

            // pre-format string
            string str = string.Format(log, args);

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var file = new StreamWriter(logFile, true))
                    {
                        file.WriteLine(
                            "{0}\t\tMVUT DETAILS: " + str,
                            DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                        break;
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// Cleans up unmanaged resources
        /// </summary>
        public void Dispose()
        {
            if (!this.handle.IsClosed)
            {
                // shutdown muFAT proxy
                this.GetDelegate<NativeMethods.Shutdown>("Shutdown").Invoke();

                // call FreeLibrary on handle
                this.handle.Close();
            }
        }

        #region Test case execution

        /// <summary>
        /// Runs a test case step as defined in the run configuration file
        /// </summary>
        /// <param name="step">Structure defining the parameters of the test step</param>
        /// <returns>Whether the step passed or failed</returns>
        [HandleProcessCorruptedStateExceptions]
        public bool Run(Step step)
        {
            if (this.handle.IsInvalid)
            {
                throw new Exception("Proxy DLL is not loaded!");
            }

            // lookup all function pointers required to run a step
            var getTestId = this.GetDelegate<NativeMethods.GetUTID>("GetUTID");
            var genericCanExecute = this.GetDelegate<NativeMethods.GenericCanExecute>("GenericCanExecute");
            var genericExecute = this.GetDelegate<NativeMethods.GenericExecute>("GenericExecute");
            var threadedExecute = this.GetDelegate<NativeMethods.ThreadedExecute>("ThreadedExecute");
            var getResult = this.GetDelegate<NativeMethods.GetThreadedExecuteResult>("GetThreadedExecuteResult");
            var submitClass = this.GetDelegate<NativeMethods.SubmitClass>("SubmitClass");

            // start timer
            Stopwatch stopwatch = Stopwatch.StartNew();
            int result = 0;

            try
            {
                // get internal test stub ID defined in Arm
                int testNum = getTestId(step.Name, step.Name.Length + 1);
                if (testNum == -1)
                {
                    throw new Exception("Test stub not found for " + step.Name);
                }

                // load the handler class from class factory and verify that it can run
                IntPtr obj = this.GetDelegate<NativeMethods.GetClass>("GetClass")(
                    step.ClassName, step.ClassName.Length + 1, this.baseObject);
                if (!genericCanExecute(obj, testNum))
                {
                    throw new Exception("Cannot get class object " + step.ClassName);
                }

                // run test stub, repeating as many times as defined
                for (int i = 0; i <= step.Repeat; i++)
                {
                    if (step.Name.Contains("SaveTillDone") ||
                        step.Name.Contains("PreviewTillDone") ||
                        step.Name.Contains("AnalyseTillDone"))
                    {
                        // use threaded version so we can monitor time elapsed
                        threadedExecute(obj, testNum, step.ConfigFile, step.ConfigFile.Length + 1);
                        int timeout = this.Timeout;

                        // poll for results - returns false if results are not yet available
                        while (!getResult(out result))
                        {
                            if (--timeout < 0)
                            {
                                throw new TimeoutException(this.Timeout + " seconds reached");
                            }

                            Thread.Sleep(1000);
                        }
                    }
                    else
                    {
                        result = genericExecute(obj, testNum, step.ConfigFile, step.ConfigFile.Length + 1);
                    }

                    submitClass(step.ClassName, step.ClassName.Length + 1, obj, this.baseObject);
                    if (result <= 0)
                    {
                        // stop executing loop if step failed
                        break;
                    }
                    
                    if (step.Repeat > 0)
                    {
                        // pause momentarily
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception e)
            {
                Log("Exception caught during {0}: {1}", step.Name, e.ToString());
                throw;
            }
            finally
            {
                // record time taken for this step
                stopwatch.Stop();
                step.Passed = result > 0;
                step.TimeTaken = stopwatch.Elapsed.TotalMilliseconds;
            }

            return result > 0;
        }

        /// <summary>
        /// Runs all test case steps as defined in the run file
        /// </summary>
        public void RunAll()
        {
            foreach (Step step in this.Steps)
            {
                if (this.Run(step))
                {
                    this.Passed++;
                }
                else
                {
                    this.Failed++;
                }
            }
        }

        /// <summary>
        /// Dumps a text summary of the test cases' execution results into a file
        /// </summary>
        /// <param name="fileName">Output filename to write the report to</param>
        public void DumpReport(string fileName)
        {
            Log("Dumping report");
            using (var file = new StreamWriter(fileName))
            {
                file.WriteLine("time:" + this.StartTime.ToLocalTime().ToString("MM-dd-yyyy, HH:mm:ss"));
                file.WriteLine("passes:" + this.Passed);
                file.WriteLine("failures:" + this.Failed);
                file.WriteLine("untested:" + (this.Steps.Count - this.Passed - this.Failed) + Environment.NewLine);

                foreach (Step step in this.Steps)
                {
                    file.WriteLine(step.Name);
                    file.WriteLine(step.ConfigFile);
                    file.WriteLine(step.Passed ? "1" : "0");
                    file.WriteLine(step.TimeTaken + Environment.NewLine);
                }
            }
        }

        #endregion

        /// <summary>
        /// Dynamically looks up a function from the loaded proxy DLL using
        /// <code>GetProcAddress</code>
        /// </summary>
        /// <typeparam name="T">Delegate of the function signature</typeparam>
        /// <param name="function">Function name to load</param>
        /// <returns>Instantiated delegate for the function pointer</returns>
        private T GetDelegate<T>(string function) where T : class
        {
            // handle not loaded or has been GC-ed
            if (this.handle.IsInvalid)
            {
                throw new Win32Exception("DLL handle has already been released");
            }
            
            if (!typeof(T).IsSubclassOf(typeof(Delegate)))
            {
                throw new ArgumentException(typeof(T).Name + " is not a valid delegate");
            }

            // find function pointer in DLL
            var ptr = NativeMethods.GetProcAddress(this.handle, function);
            if (ptr == IntPtr.Zero)
            {
                throw new Win32Exception("Could not resolve function " + function);
            }

            // hack: can't apply Delegate constraint to T, so have to cast from object
            object invoker = Marshal.GetDelegateForFunctionPointer(
                ptr, typeof(T));
            return (T)invoker;
        }

        #region Configuration file parsing

        /// <summary>
        /// Processes a closure block inside a .run configuration file
        /// </summary>
        /// <param name="enumerator">Enumerator reference to a current position
        /// inside an enumerable list of instruction strings</param>
        /// <returns>Stack of instruction strings from this closure block</returns>
        private IEnumerable<string> ParseClosure(ref IEnumerator<string> enumerator)
        {
            // stack of instruction strings in this block
            var closure = new List<string>();
            int count = 0;

            // keep reading until we find end of block
            while (enumerator.MoveNext())
            {
                var next = enumerator.Current;
                if (next.Length == 0)
                {
                    continue;
                }
                
                if (next[0] == '(' && enumerator.MoveNext())
                {
                    // new closure block - recursive processing required
                    closure.AddRange(this.ParseClosure(ref enumerator));
                }
                else if (next[0] == ')')
                {
                    // end of block found
                    if (next[1] == '*')
                    {
                        // number of times to repeat block
                        int.TryParse(next.Substring(2), out count);
                    }

                    break;
                }
                else
                {
                    closure.Add(next);
                }
            }

            // if block is to be repeated >1 times, duplicate all strings in the stack
            var result = closure as IEnumerable<string>;
            for (int i = 1; i < count; i++)
            {
                result = result.Concat(closure);
            }

            // return processed stack of instructions
            return result;
        }

        /// <summary>
        /// Preprocesses a .run configuration file into an instruction list
        /// </summary>
        /// <param name="config">Run configuration file</param>
        /// <returns>Stack of instruction strings to be processed</returns>
        private IEnumerable<string> ParseConfig(string config)
        {
            // verify that closures are defined correctly
            string configText = File.ReadAllText(config);
            if ((configText.Length - configText.Replace("(", string.Empty).Length) !=
                (configText.Length - configText.Replace(")", string.Empty).Length))
            {
                throw new Exception("Mismatched number of braces");
            }

            var stack = new List<string>();
            var lines = configText.Split(Environment.NewLine.ToCharArray())
                            .Select(s => s.Trim()).ToList();
            var enumerator = lines.GetEnumerator() as IEnumerator<string>;
            while (enumerator.MoveNext())
            {
                string line = enumerator.Current;
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '(')
                {
                    // closure block found; pass reference to enumerator
                    stack.AddRange(this.ParseClosure(ref enumerator));
                }
                else if (line[0] == ';')
                {
                    // could be a comment line or header instruction
                    string[] tokens = line.Substring(1).Split(
                        new[] { "::" },
                        StringSplitOptions.None);

                    // INCLUDE instruction found
                    if (tokens.Length > 1 && tokens[0].Trim().ToUpper() == "INCLUDE")
                    {
                        // process relative paths
                        string fileName = tokens[1].Trim();
                        if (!File.Exists(fileName))
                        {
                            string dir = Path.GetDirectoryName(config);
                            Debug.Assert(dir != null, "dir != null");
                            fileName = Path.GetFullPath(Path.Combine(dir, fileName));
                        }

                        // recursively process included file
                        stack.AddRange(this.ParseConfig(fileName));
                    }
                }
                else if (line.Split(',').Length > 3)
                {
                    throw new Exception("Invalid number of tokens: " + line);
                }
                else
                {
                    stack.Add(line);
                }
            }

            // return processed stack of instructions
            return stack;
        }

        /// <summary>
        /// Parse a .run configuration file describing a full test run
        /// </summary>
        /// <param name="config">Run configuration file</param>
        private void Parse(string config)
        {
            // pre-process config file into list of step instructions
            foreach (string line in this.ParseConfig(config))
            {
                // empty line
                var tokens = line.Split(',').Select(s => s.Trim()).ToArray();
                if (tokens.Length < 1)
                {
                    continue;
                }

                // first token describes class/interface/function
                var commands = tokens[0].Split(
                    new[] { "__" },
                    StringSplitOptions.None);

                // resolve relative path by resources folder
                string fileName = tokens[1];
                if (!File.Exists(fileName))
                {
                    fileName = Path.Combine(ResourcesPath, tokens[0], fileName);
                }

                // still doesn't exist - it's not a filename
                if (!File.Exists(fileName))
                {
                    fileName = string.Empty;
                }

                // create step structure
                var step = new Step
                {
                    Name = tokens[0],
                    ClassName = commands[1],
                    InterfaceName = commands[2],
                    FunctionName = commands[3],
                    ConfigFile = fileName
                };

                // repeat times
                if (tokens.Length > 2 && tokens[2].Length > 0)
                {
                    step.Repeat = int.Parse(tokens[2]);
                }

                this.Steps.Add(step);
            }
        }

        #endregion

        /// <summary>
        /// Wrapper class defining a test case step
        /// </summary>
        public class Step
        {
            /// <summary>
            /// Initializes a new instance of the Step class
            /// </summary>
            public Step()
            {
                this.Repeat = 0;
                this.Passed = false;
                this.TimeTaken = 0;
            }

            /// <summary>
            /// Gets or sets the name of test case
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the class name
            /// </summary>
            public string ClassName { get; set; }

            /// <summary>
            /// Gets or sets the interface name
            /// </summary>
            public string InterfaceName { get; set; }

            /// <summary>
            /// Gets or sets the function name
            /// </summary>
            public string FunctionName { get; set; }

            /// <summary>
            /// Gets or sets the config file path
            /// </summary>
            public string ConfigFile { get; set; }

            /// <summary>
            /// Gets or sets the number of times to repeat test
            /// </summary>
            public int Repeat { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the test passed/failed
            /// </summary>
            public bool Passed { get; set; }

            /// <summary>
            /// Gets or sets the milliseconds elapsed for test to complete
            /// </summary>
            public double TimeTaken { get; set; }
        }
    }
}
