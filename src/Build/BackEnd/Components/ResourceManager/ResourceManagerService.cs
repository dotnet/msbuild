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
        //ILoggingService? _loggingService;

        public int TotalNumberHeld = -1;
        public int Count = 0;

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskResourceManager, "Cannot create components of type {0}", type);

            return new ResourceManagerService();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {

        }

        public void ShutdownComponent()
        {
            //_loggingService = null;

            TotalNumberHeld = -2;
        }

        public int? RequestCores(int requestedCores, TaskLoggingContext _taskLoggingContext)
        {
            return null;

            // _loggingService?.LogComment(_taskLoggingContext.BuildEventContext, Framework.MessageImportance.Low, "ResourceManagerRequestedCores", requestedCores, i, TotalNumberHeld);
        }

        public void ReleaseCores(int coresToRelease, TaskLoggingContext _taskLoggingContext)
        {
            ErrorUtilities.VerifyThrow(coresToRelease > 0, "Tried to release {0} cores", coresToRelease);
            return;

            //_loggingService?.LogComment(_taskLoggingContext.BuildEventContext, Framework.MessageImportance.Low, "ResourceManagerReleasedCores", coresToRelease, TotalNumberHeld);
        }
    }
}
