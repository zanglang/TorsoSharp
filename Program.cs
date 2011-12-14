//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;
    using System.IO;
    using SysInfoSharp;

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
            string configFile = @"y:\mufat\testruns\regressionpaths\baseline2.run";
            string proxy = @"C:\Users\jerry\Documents\Projects\trunk2\output\binaries\muFAT\SDKRuntime\muFATProxyD.dll";
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
                        }
                        break;
                    case "/RES":
                        if (args.Length > i + 1)
                        {
                            Torso.ResourcesPath = args[++i];
                        }
                        break;
                    case "/PROXYPATH":
                        if (args.Length > i + 1)
                        {
                            proxy = args[++i];
                        }
                        break;
                    case "/TIMEOUT":
                        if ((args.Length > i + 1) &&
                            Int32.TryParse(args[++i], out timeout) &&
                            timeout <= 0)
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

                    case "/?":
                    case "--help":
                    default:
                        Console.WriteLine(@"Parameters:
-------------------------------------------------------
[required] /c         'Full path to config file'
[required] /res       'Full path to config storage'
[optional] /proxypath 'Full path to proxy dll'
[optional] /timeout   'Maximum seconds until test is aborted'
[optional] /debugbrk  'Sets a breakpoint before running tests for debugging'");
                        return;
                }
            }

            if (!Directory.Exists(@"C:\muveeDebug"))
            {
                Directory.CreateDirectory(@"C:\muveeDebug");
            }

            // dumping system info
            SysInfoLib sysInfo = new SysInfoLib();
            sysInfo.Init();
            using (StreamWriter file = new StreamWriter(
                Path.Combine(@"C:\muveeDebug", "sysinfoout.txt")))
            {
                foreach (string category in sysInfo.GetCategories())
                {
                    file.WriteLine("--------------------------------------------");
                    file.WriteLine("Category: " + category);
                    file.WriteLine("--------------------------------------------");

                    foreach (var pair in sysInfo[category])
                    {
                        file.WriteLine("\t{0}={1}", pair.Key, pair.Value);
                    }
                }
            }

            using (Torso t = new Torso(configFile, proxy))
            {
                if (debug)
                {
                    System.Diagnostics.Debugger.Break();
                }

                // set timeout if provided
                if (timeout > 0)
                {
                    t.Timeout = timeout;
                }

                try
                {
                    t.RunAll();
                }
                catch (Exception e)
                {
                    t.Log("Torso terminated: " + e);
                }
                finally
                {
                    t.Log("Passes {0}, Failures {1}, Untested {2}",
                        t.Passed, t.Failed, t.Steps.Count - t.Passed - t.Failed);

                    // dump summary report
                    string testName = Path.GetFileNameWithoutExtension(configFile);
                    string report = Path.Combine(@"C:\muveeDebug", testName + ".txt");
                    t.DumpReport(report);

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
