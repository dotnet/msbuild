// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.ZipDeploy
{
    public enum DeployStatus
    {
        Unknown = -1,
        Pending = 0,
        Building = 1,
        Deploying = 2,
        Failed = 3,
        Success = 4
    }
}
