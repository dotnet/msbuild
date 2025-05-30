// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Class for executing a task in an AppDomain
    /// </summary>
    [Serializable]
    internal class OutOfProcTaskAppDomainWrapper : OutOfProcTaskAppDomainWrapperBase
    {
        /// <summary>
        /// This is an extension of the OutOfProcTaskAppDomainWrapper that is responsible
        /// for activating and executing the user task.
        /// This extension provides support for ICancellable Out-Of-Proc tasks.
        /// </summary>
        /// <returns>True if the task is ICancellable</returns>
        internal bool CancelTask()
        {
            // If the cancel was issued even before WrappedTask has been created then set a flag so that we can
            // skip execution
            CancelPending = true;

            // Store in a local to avoid a race
            var wrappedTask = WrappedTask;
            if (wrappedTask == null)
            {
                return true;
            }

            ICancelableTask cancelableTask = wrappedTask as ICancelableTask;
            if (cancelableTask != null)
            {
                cancelableTask.Cancel();
                return true;
            }

            return false;
        }
    }
}
