// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class WindowsOnlyRequiresMSBuildVersionFactAttribute : FactAttribute
    {
        public WindowsOnlyRequiresMSBuildVersionFactAttribute(string version)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Skip = "This test requires Windows to run";
            }

            RequiresMSBuildVersionTheoryAttribute.CheckForRequiredMSBuildVersion(this, version);
        }
    }
}
