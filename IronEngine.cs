//-----------------------------------------------------------------------
// <copyright file="IronEngine.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using IronPython.Hosting;
    using Microsoft.Scripting;
    using Microsoft.Scripting.Hosting;
    using MVRuntimeLib;

    /// <summary>
    /// IronEngine Script Executor
    /// </summary>
    public class IronEngine
    {
        /// <summary>
        /// ScriptEngine instance
        /// </summary>
        public ScriptEngine Engine { get; private set; }

        /// <summary>
        /// Scope of currently initialized engine
        /// </summary>
        public ScriptScope Scope { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public IronEngine()
        {
            // allow access to previous stack frames
            var options = new Dictionary<string, object>();
            options["Frames"] = true;

            // create script engine
            this.Engine = Python.CreateEngine(options);
            this.Scope = this.Engine.CreateScope();
            this.Scope.SetVariable("__name__", "__main__");
            
            // set up search paths for common Python modules
            if (Directory.Exists(@"C:\Program Files (x86)\IronPython 2.7\Lib"))
            {
                AddSearchPath(@"C:\Program Files (x86)\IronPython 2.7\Lib");
            }

            // precompressed ironpython libraries
            if (File.Exists("packages.zip"))
            {
                AddSearchPath("packages.zip");
            }

            AddSearchPath(Environment.CurrentDirectory);

            // add CLR reference to IronBindings class
            var asm = Assembly.GetAssembly(typeof(IronBindings));
            this.Engine.Runtime.LoadAssembly(asm);
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~IronEngine()
        {
            // don't have anything important to do for now
            Console.WriteLine("Cleaning up.");
        }

        /// <summary>
        /// Adds a filesystem path to the IronPython script engine's search path
        /// </summary>
        /// <param name="path">Path to a IronPython library module, or zip file</param>
        public void AddSearchPath(string path)
        {
            var paths = this.Engine.GetSearchPaths();
            if (string.IsNullOrEmpty(path) ||
                (!File.Exists(path) && !Directory.Exists(path)))
            {
                throw new Exception("Path not valid!");
            }

            // add new path to existing paths
            paths.Add(path);
            this.Engine.SetSearchPaths(paths);
        }

        /// <summary>
        /// Executes a block of code
        /// </summary>
        /// <param name="code">String of Python code to run</param>
        /// <returns>Any dynamic result object returned by the code</returns>
        public dynamic ExecuteCode(string code)
        {
            var source = this.Engine.CreateScriptSourceFromString(code, SourceCodeKind.Statements);
            var compiled = source.Compile();
            return compiled.Execute(this.Scope);
        }

        /// <summary>
        /// Executes a Python script file
        /// </summary>
        /// <param name="path">Filesystem path to a runnable Python file</param>
        /// <returns>Any dynamic result object returned by the script</returns>
        public dynamic Execute(string path)
        {
            if (!File.Exists(path))
            {
                throw new IOException("Path not found for " + path);
            }

            // so that the script engine knows where to load adjacent modules
            this.AddSearchPath(Path.GetDirectoryName(path));

            var source = this.Engine.CreateScriptSourceFromFile(path, Encoding.UTF8, SourceCodeKind.Statements);
            var compiled = source.Compile();
            return compiled.Execute(this.Scope);
        }
    }
}
