// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
//-----------------------------------------------------------------------

using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Framework.Profiler
{
    /// <summary>
    /// Assigns unique evaluation ids. Thread safe.
    /// </summary>
    public static class EvaluationIdProvider
    {
        private static int _sAssignedId = -1;
        private static readonly int ProcessId = Process.GetCurrentProcess().Id;

        /// <summary>
        /// Returns a unique evaluation id
        /// </summary>
        /// <remarks>
        /// The id is guaranteed to be unique across all running processes
        /// </remarks>
        public static int GetNextId()
        {
            var nextId = Interlocked.Increment(ref _sAssignedId);
            // Returns a unique number based on nextId (a unique number for this process) and the current process Id
            // Uses the Cantor pairing function (https://en.wikipedia.org/wiki/Pairing_function) to guarantee uniqueness
            return (((nextId + ProcessId) * (nextId + ProcessId + 1)) / 2) + ProcessId;
        }
    }
}
