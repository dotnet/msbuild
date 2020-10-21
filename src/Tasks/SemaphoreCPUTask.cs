// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Tasks
{
    class SemaphoreCPUTask : Task
    {
        private const int Repetitions = 20;

        public override bool Execute()
        {
            Log.LogMessageFromText($"Starting in {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);

            BuildEngine8.Yield();

            //int initial = BuildEngine7.RequestCores(3123890);
            //Log.LogMessageFromText($"Got {initial} cores from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);

            //if (initial > 0)
            //{
            //    while (initial > 0)
            //    {
            //        Thread.Sleep(TimeSpan.FromSeconds(1));
            //        BuildEngine7.ReleaseCores(1);
            //        initial--;
            //        Log.LogMessageFromText($"Released 1 core from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);
            //    }

            //    return !Log.HasLoggedErrors;
            //}

            //for (int i = 0; i < 20; i++)
            //{
            //    int v = BuildEngine7.RequestCores(9999);
            //    Log.LogMessageFromText($"Got {v} cores  from {System.Diagnostics.Process.GetCurrentProcess().Id}", Framework.MessageImportance.High);
            //    BuildEngine7.ReleaseCores(v + 20);
            //    Thread.Sleep(TimeSpan.FromSeconds(0.9));
            //}

            System.Threading.Tasks.Task[] tasks = new System.Threading.Tasks.Task[Repetitions];

            for (int i = 0; i < Repetitions; i++)
            {
                int i_local = i;
                tasks[i] = System.Threading.Tasks.Task.Run(() => LaunchAndComplete(i_local, () => BuildEngine8.ReleaseCores(1)));
            }

            System.Threading.Tasks.Task.WhenAll(tasks).Wait();

            BuildEngine8.Reacquire();

            return !Log.HasLoggedErrors;
        }

        void LaunchAndComplete(int i, Action completionCallback)
        {
            Stopwatch s = new Stopwatch();
            s.Start();
            BuildEngine8.RequestCores(1);
            Log.LogMessageFromText($"Action {i} started from {System.Diagnostics.Process.GetCurrentProcess().Id}, waited {s.Elapsed}", Framework.MessageImportance.High);
            Thread.Sleep(2_000);
            Log.LogMessageFromText($"Action {i} completed from {System.Diagnostics.Process.GetCurrentProcess().Id}, total {s.Elapsed}", Framework.MessageImportance.High);

            completionCallback.Invoke();
        }
    }
}
