// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Threading;

namespace Microsoft.Build.Tasks
{
    class SemaphoreCPUTask : Task
    {
        public override bool Execute()
        {
            int initial = BuildEngine7.RequestCores(3123890);
            Log.LogMessageFromText($"Got {initial} cores from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);

            if (initial > 0)
            {
                while (initial > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    BuildEngine7.ReleaseCores(1);
                    initial--;
                    Log.LogMessageFromText($"Released 1 core from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);
                }

                return !Log.HasLoggedErrors;
            }

            for (int i = 0; i < 20; i++)
            {
                int v = BuildEngine7.RequestCores(9999);
                Log.LogMessageFromText($"Got {v} cores  from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);
                BuildEngine7.ReleaseCores(v + 20);
                Thread.Sleep(TimeSpan.FromSeconds(0.9));
            }

            return !Log.HasLoggedErrors;
        }
    }
}
