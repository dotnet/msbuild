// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using System.Threading;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.ResourceManager
{
    class ResourceManagerService : IBuildComponent
    {
        Semaphore? s = null;

        ILoggingService? _loggingService;

        public int TotalNumberHeld = -1;
        public int Count = 0;

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskResourceManager, "Cannot create components of type {0}", type);

            return new ResourceManagerService();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            if (NativeMethodsShared.IsWindows)
            {
                string semaphoreName = host.BuildParameters.ResourceManagerSemaphoreName;

                int resourceCount = host.BuildParameters.MaxNodeCount + Traits.Instance.ResourceManagerOversubscription;

                Count = resourceCount;

                _loggingService = host.LoggingService;

                TotalNumberHeld = 0;

                s = new Semaphore(resourceCount, resourceCount, semaphoreName); // TODO: SemaphoreSecurity?
            }
            else
            {
                // UNDONE: just don't support gathering additional cores on non-Windows
                s = null;
            }

        }

        public void ShutdownComponent()
        {
            s?.Dispose();
            s = null;

            _loggingService = null;

            TotalNumberHeld = -2;
        }

        public int? RequestCores(int requestedCores, TaskLoggingContext _taskLoggingContext)
        {
            if (s is null)
            {
                return null;
            }

            int i;

            // Keep requesting cores until we can't anymore, or we've gotten the number of cores we wanted.
            for (i = 0; i < requestedCores; i++)
            {
                if (!s.WaitOne(0))
                {
                    break;
                }
            }

            TotalNumberHeld += i;

            _loggingService?.LogComment(_taskLoggingContext.BuildEventContext, Framework.MessageImportance.Low, "ResourceManagerRequestedCores", requestedCores, i, TotalNumberHeld);

            return i;
        }

        public void ReleaseCores(int coresToRelease, TaskLoggingContext _taskLoggingContext)
        {
            if (s is null)
            {
                // Since the current implementation of the cross-process resource count uses
                // named semaphores, it's not usable on non-Windows, so just continue.
                return;
            }

            ErrorUtilities.VerifyThrow(coresToRelease > 0, "Tried to release {0} cores", coresToRelease);

            if (coresToRelease > TotalNumberHeld)
            {
                _loggingService?.LogWarning(_taskLoggingContext.BuildEventContext, null, null, "ResourceManagerExcessRelease", coresToRelease);

                coresToRelease = TotalNumberHeld;
            }

            s.Release(coresToRelease);

            TotalNumberHeld -= coresToRelease;

            _loggingService?.LogComment(_taskLoggingContext.BuildEventContext, Framework.MessageImportance.Low, "ResourceManagerReleasedCores", coresToRelease, TotalNumberHeld);
        }
    }
}
