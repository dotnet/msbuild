// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.ResourceManager
{
    class ResourceManagerService : IBuildComponent
    {
        Semaphore? s = null;

        public int TotalNumberHeld = -1;

        private static StringBuilder log = new StringBuilder();

        public void Log(string s) => log.AppendFormat("{0}: {1}, current={2} thread={4} {3}", DateTime.Now.Ticks, s, TotalNumberHeld, Environment.NewLine, Thread.CurrentThread.ManagedThreadId);

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskResourceManager, "Cannot create components of type {0}", type);

            return new ResourceManagerService();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            const string SemaphoreName = "cpuCount"; // TODO

            int resourceCount = host.BuildParameters.MaxNodeCount; // TODO: tweakability

            TotalNumberHeld = 0;

            Log($"Initialized with {resourceCount}");

            s = new Semaphore(resourceCount, resourceCount, SemaphoreName); // TODO: SemaphoreSecurity?
        }

        public void ShutdownComponent()
        {
            s?.Dispose();
            s = null;

            Log($"Tearing down; held should have been {TotalNumberHeld}");

            TotalNumberHeld = -2;
        }

        public int RequestCores(int requestedCores)
        {
            Log($"Requesting {requestedCores}");

            if (s is null)
            {
                // TODO: ErrorUtilities should be annotated so this can just be `ErrorUtilities.VerifyThrow`
                // https://github.com/microsoft/msbuild/issues/5163
                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
            }

            int i = 0;

            // Keep requesting cores until we can't anymore, or we've gotten the number of cores we wanted.
            for (i = 0; i < requestedCores; i++)
            {
                if (!s.WaitOne(0))
                {
                    return i;
                }
            }

            Log($"got {i}, holding {TotalNumberHeld}");

            return i;
        }

        public void ReleaseCores(int coresToRelease)
        {
            if (s is null)
            {
                // TODO: ErrorUtilities should be annotated so this can just be `ErrorUtilities.VerifyThrow`
                // https://github.com/microsoft/msbuild/issues/5163
                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
            }

            ErrorUtilities.VerifyThrow(coresToRelease > 0, "Tried to release {0} cores", coresToRelease);

            s.Release(coresToRelease);

            TotalNumberHeld -= coresToRelease;

            Log($"released {coresToRelease}, now holding {TotalNumberHeld}");
        }

        internal void RequireCores(int requestedCores)
        {
            if (s is null)
            {
                // TODO: ErrorUtilities should be annotated so this can just be `ErrorUtilities.VerifyThrow`
                // https://github.com/microsoft/msbuild/issues/5163
                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
            }

            if (TotalNumberHeld >= 1)
            {
                //Debugger.Launch();
            }

            if (!s.WaitOne())
            {
                ErrorUtilities.ThrowInternalError("Couldn't get a core to run a task even with infinite timeout");
            }

            TotalNumberHeld++;
            Log($"required 1, now holding {TotalNumberHeld}");
        }
    }
}
