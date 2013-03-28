//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Windows.Forms;
    using Microsoft.Win32;

    /// <summary>
    /// Main program
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Program entry function
        /// </summary>
        /// <param name="args">Command line arguments</param>
        [STAThread]
        public static void Main(string[] args)
        {
            string configFile = string.Empty;
            string proxy = string.Empty;
            int timeout = -1;
            bool debug = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToUpper())
                {
                    case "/C":
                        if (args.Length > i + 1)
                        {
                            configFile = args[++i];
                            if (!File.Exists(configFile))
                            {
                                throw new IOException(configFile + " does not exist!");
                            }
                        }

                        break;
                    case "/PROXYPATH":
                        if (args.Length > i + 1)
                        {
                            proxy = args[++i];
                        }

                        break;
                    case "/TIMEOUT":
                        if ((args.Length > i + 1) && int.TryParse(args[++i], out timeout) && timeout <= 0)
                        {
                            throw new ArgumentException("Timeout must be >1");
                        }

                        break;

                    case "/S":
                        // option ignored; only kept for compatibility with old app
                        break;

                    case "/DEBUGBRK":
                        debug = true;
                        break;

                    default:
                        if (File.Exists(args[i]))
                        {
                            configFile = args[i];
                            break;
                        }

                        Console.WriteLine(
                            @"Parameters:
-------------------------------------------------------
[required] /c         'Full path to config file'
[required] /res       'Full path to config storage'
[optional] /proxypath 'Full path to proxy dll'
[optional] /timeout   'Maximum seconds until test is aborted'
[optional] /debugbrk  'Sets a breakpoint before running tests for debugging'");
                        return;
                }
            }

            if (string.IsNullOrEmpty(configFile))
            {
                throw new Exception("Config file is not set!");
            }

            if (Path.GetExtension(configFile) != ".py" && string.IsNullOrEmpty(proxy))
            {
                throw new Exception("Proxy DLL is not set!");
            }

            if (!Directory.Exists(@"C:\muveeDebug"))
            {
                Directory.CreateDirectory(@"C:\muveeDebug");
            }

            // set up exception handling/logging
            AppDomain.CurrentDomain.UnhandledException +=
                (e, eargs) => Torso.Log("Unhandled exception: " + eargs.ExceptionObject.ToString());

            if (debug)
            {
                MessageBox.Show("Attach a debugger now.");
            }

            using (var t = (Path.GetExtension(configFile) == ".py")
                ? (ITorso)new IronTorso(configFile) : new Torso(configFile, proxy))
            {

                // set timeout if provided
                if (timeout > 0)
                {
                    t.Timeout = timeout;
                }

                // log current test name in registry
                var testName = Path.GetFileNameWithoutExtension(configFile);
                try
                {
                    using (
                        var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\muvee Technologies\muFAT\Torso"))
                    {
                        Debug.Assert(key != null, "key != null");
                        Debug.Assert(testName != null, "testName != null");
                        key.SetValue("[ConfigName]", testName);
                    }

                    t.RunAll();
                }
                catch (Exception e)
                {
                    Torso.Log("Torso terminated: " + e);
                }
                finally
                {
                    Torso.Log(
                        "Passes {0}, Failures {1}, Untested {2}",
                        t.Passed,
                        t.Failed,
                        t.Skipped);

                    // dump summary report
                    string report = Path.Combine(@"C:\muveeDebug", testName + ".txt");
                    t.DumpReport(report);

                    // delete subkey
                    using (var key = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\muvee Technologies\muFAT",
                        true))
                    {
                        Debug.Assert(key != null, "key != null");
                        key.DeleteSubKeyTree("Torso", false);
                    }

                    // wait for log.txt to finish writing
                    Thread.Sleep(1000);

                    // copy log file
                    if (File.Exists(@"C:\muveeDebug\Log.txt"))
                    {
                        File.Copy(
                            @"C:\muveeDebug\Log.txt",
                            Path.Combine(@"C:\muveeDebug", testName + "_Log.txt"),
                            true);
                    }

                    if (File.Exists(@"C:\muveeDebug\SDKTrace.log"))
                    {
                        File.Copy(
                            @"C:\muveeDebug\SDKTrace.log",
                            Path.Combine(@"C:\muveeDebug", testName + "_SDKTrace.txt"),
                            true);
                    }
                }
            }
        }
    }
}
