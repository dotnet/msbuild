// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Workloads.Workload.Install
{
    internal class WorkloadInstallerFactory
    {
        public static IWorkloadInstaller GetWorkloadInstaller(IReporter reporter)
        {
            return new NetSdkManagedInstaller(reporter);
        }
    }
}
