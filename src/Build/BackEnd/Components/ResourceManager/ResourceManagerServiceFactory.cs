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
    class ResourceManagerServiceFactory
    {
        public IBuildComponent CreateInstance(BuildComponentType type)
        {
            // Create the instance of OutOfProcNodeSdkResolverService and pass parameters to the constructor.
            return new ResourceManagerService();
        }

    }
}
