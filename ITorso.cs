//-----------------------------------------------------------------------
// <copyright file="ITorso.cs" company="muvee Technologies Pte Ltd">
//   Copyright (c) muvee Technologies Pte Ltd. All rights reserved.
// </copyright>
// <author>Jerry Chong</author>
//-----------------------------------------------------------------------

namespace Torso
{
    using System;

    /// <summary>
    /// Interface for Torso classes
    /// </summary>
    public interface ITorso : IDisposable
    {
        /// <summary>
        /// Gets the number of passed runs
        /// </summary>
        int Passed { get; }

        /// <summary>
        /// Gets the number of failed runs
        /// </summary>
        int Failed { get; }

        /// <summary>
        /// Gets the number of skipped runs
        /// </summary>
        int Skipped { get; }

        /// <summary>
        /// Gets or sets the number of seconds to wait until a test is considered timed out
        /// </summary>
        int Timeout { get; set; }
        
        /// <summary>
        /// Runs all test case steps as defined in the run file
        /// </summary>
        void RunAll();

        /// <summary>
        /// Dumps a text summary of the test cases' execution results into a file
        /// </summary>
        /// <param name="fileName">Output filename to write the report to</param>
        void DumpReport(string fileName);
    }
}