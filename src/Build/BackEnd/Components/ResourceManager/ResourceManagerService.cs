// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
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

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskResourceManager, "Cannot create components of type {0}", type);

            return new ResourceManagerService();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            const string SemaphoreName = "cpuCount"; // TODO

            int resourceCount = host.BuildParameters.MaxNodeCount; // TODO: tweakability

            s = new Semaphore(resourceCount, resourceCount, SemaphoreName); // TODO: SemaphoreSecurity?
        }

        public void ShutdownComponent()
        {
            s?.Dispose();
            s = null;
        }

        public int RequestCores(int requestedCores)
        {
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
        }

        internal void RequireCores(int requestedCores)
        {
            if (s is null)
            {
                // TODO: ErrorUtilities should be annotated so this can just be `ErrorUtilities.VerifyThrow`
                // https://github.com/microsoft/msbuild/issues/5163
                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
            }

            if (!s.WaitOne())
            {
                ErrorUtilities.ThrowInternalError("Couldn't get a core to run a task even with infinite timeout");

            }
        }
    }
}
