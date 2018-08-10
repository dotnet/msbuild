// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class EnvironmentInfo
    {
        public static bool HasSharedFramework(string framework)
        {
            string rid = RepoDirectoriesProvider.BuildRid;

            if (framework == "netcoreapp1.0")
            {
                switch (rid)
                {
                    case "fedora.24-x64":
                    case "rhel.6-x64":
                    case "linux-musl-x64":
                    case "opensuse.42.1-x64":
                    case "ubuntu.16.10-x64":
                    case "linux-x64":
                        return false;
                }
            }
            else if (framework == "netcoreapp1.1")
            {
                switch (rid)
                {
                    case "linux-x64":
                    case "rhel.6-x64":
                    case "linux-musl-x64":
                        return false;
                }
            }
            else if (framework == "netcoreapp2.0")
            {
                switch (rid)
                {
                    case "rhel.6-x64":
                    case "linux-musl-x64":
                        return false;
                }
            }

            return true;
        }
    }
}
