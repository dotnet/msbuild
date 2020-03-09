// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;

namespace Microsoft.Build.CommandLine
{
    internal class PriorityUtils
    {
        public static void SwitchProcessPriorityTo(ProcessPriorityClass priority)
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = priority;
            }
            // Changing to a higher priority can cause exceptions on certain operating systems if
            // run without administrator privileges. Swallow these exceptions.
            catch (Exception) { }
        }
    }
}
