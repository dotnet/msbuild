// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Class for executing a task in an AppDomain
    /// </summary>
    [Serializable]
    internal class OutOfProcTaskAppDomainWrapper : OutOfProcTaskAppDomainWrapperBase
    {
        /// <summary>
        /// This is a stub for CLR2 in place of the OutOfProcTaskAppDomainWrapper class
        /// as used in CLR4 to support cancellation of ICancelable tasks.
        /// We provide a stub for CancelTask here so that the OutOfProcTaskHostNode
        /// that's shared by both the MSBuild.exe and MSBuildTaskHost.exe,
        /// can safely allow MSBuild.exe CLR4 Out-Of-Proc Task Host to call ICancelableTask.Cancel()
        /// </summary>
        /// <returns>False - Used by the OutOfProcTaskHostNode to determine if the task is ICancelable</returns>
        internal bool CancelTask()
        {
            // This method is a stub we will not do anything here.
            return false;
        }
    }
}
