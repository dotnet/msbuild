// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#nullable disable

namespace Microsoft.Build.Shared
{
    internal static class ProcessExtensions
    {
        public static void KillTree(this Process process, int timeoutMilliseconds)
        {
#if NET
            process.Kill(entireProcessTree: true);
#else
            if (NativeMethodsShared.IsWindows)
            {
                try
                {
                    // issue the kill command
                    NativeMethodsShared.KillTree(process.Id);
                }
                catch (System.InvalidOperationException)
                {
                    // The process already exited, which is fine,
                    // just continue.
                }
            }
            else
            {
                throw new System.NotSupportedException();
            }
#endif
            // wait until the process finishes exiting/getting killed.
            // We don't want to wait forever here because the task is already supposed to be dieing, we just want to give it long enough
            // to try and flush what it can and stop. If it cannot do that in a reasonable time frame then we will just ignore it.
            process.WaitForExit(timeoutMilliseconds);
        }
    }
}
