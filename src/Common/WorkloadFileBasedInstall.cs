// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.DotNet.Workloads.Workload
{
    static class WorkloadFileBasedInstall
    {
        public static bool IsUserLocal(string dotnetDir, string sdkFeatureBand)
        {
            string userlocalPath = Path.Combine(dotnetDir, "metadata", "workloads", sdkFeatureBand, "userlocal");

            return File.Exists(userlocalPath);
        }
    }
}