// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Build.Execution;

namespace Microsoft.DotNet.BuildServer
{
    internal class MSBuildServerManager : IBuildServerManager
    {
        public string ServerName => LocalizableStrings.MSBuildServer;

        public Task<Result> ShutdownServerAsync()
        {
            return Task.Run(() => {
                try
                {
                    BuildManager.DefaultBuildManager.ShutdownAllNodes();
                    return new Result(ResultKind.Success);
                }
                catch (Exception ex)
                {
                    return new Result(ex);
                }
            });
        }
    }
}
