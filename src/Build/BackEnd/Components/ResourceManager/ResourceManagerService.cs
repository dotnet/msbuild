// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using System.Threading;

#nullable enable

namespace Microsoft.Build.BackEnd.Components.ResourceManager
{
    class ResourceManagerService : IBuildComponent
    {
        Semaphore? s = null;

        ILoggingService? _loggingService;

#if DEBUG
        public int TotalNumberHeld = -1;
        public string? SemaphoreName;
#endif

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskResourceManager, "Cannot create components of type {0}", type);

            return new ResourceManagerService();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            string semaphoreName = host.BuildParameters.ResourceManagerSemaphoreName;

            int resourceCount = host.BuildParameters.MaxNodeCount; // TODO: tweakability

            _loggingService = host.LoggingService;

#if DEBUG
            TotalNumberHeld = 0;
            SemaphoreName = semaphoreName;
#endif

            if (NativeMethodsShared.IsWindows)
            {
                s = new Semaphore(resourceCount, resourceCount, semaphoreName); // TODO: SemaphoreSecurity?
            }
            else
            {
                // UNDONE: just don't support gathering additional cores on non-Windows
                s = new Semaphore(1, 1);
            }
        }

        public void ShutdownComponent()
        {
            s?.Dispose();
            s = null;

            _loggingService = null;

#if DEBUG
            TotalNumberHeld = -2;
#endif
        }

        public int? RequestCores(int requestedCores, TaskLoggingContext _taskLoggingContext)
        {
            if (s is null)
            {
                if (!NativeMethodsShared.IsWindows)
                {
                    // Since the current implementation of the cross-process resource count uses
                    // named semaphores, it's not usable on non-Windows, so just return the
                    // guaranteed resource.
                    return null;
                }

                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
            }

            int i = 0;

            // First core gets a blocking wait: the user task wants to do *something*
            s.WaitOne();

            // Keep requesting cores until we can't anymore, or we've gotten the number of cores we wanted.
            for (i = 1; i < requestedCores; i++)
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
                if (!NativeMethodsShared.IsWindows)
                {
                    // Since the current implementation of the cross-process resource count uses
                    // named semaphores, it's not usable on non-Windows, so just continue.
                    return;
                }

                throw new InternalErrorException($"{nameof(ResourceManagerService)} was called while uninitialized");
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
