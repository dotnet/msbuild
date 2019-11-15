// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Build.Execution;

namespace Microsoft.DotNet.BuildServer
{
    internal class MSBuildServer : IBuildServer
    {
        public int ProcessId => 0; // Not yet used

        public string Name => LocalizableStrings.MSBuildServer;

        public void Shutdown()
        {
            BuildManager.DefaultBuildManager.ShutdownAllNodes();
        }
    }
}
