// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;

namespace Microsoft.Build.CommandLine
{
    internal class PriorityUtils
    {
        private static ProcessPriorityClass? _initialPriorityClass = null;

        public static DisposablePriority SwitchProcessPriorityTo(ProcessPriorityClass priority)
        {
            _initialPriorityClass ??= Process.GetCurrentProcess().PriorityClass;
            Process.GetCurrentProcess().PriorityClass = priority;
            return new DisposablePriority();
        }

        private static void ResetPriority()
        {
            Process.GetCurrentProcess().PriorityClass = _initialPriorityClass ?? ProcessPriorityClass.Normal;
        }

        internal class DisposablePriority : IDisposable
        {
            public void Dispose()
            {
                ResetPriority();
            }
        }
    }
}
