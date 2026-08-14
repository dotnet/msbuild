// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    // Central flag for whether tests are running.  The test host (TestAssemblyInfo) sets this to
    //  true at startup; BuildEnvironmentHelper, which is compiled into this same assembly
    //  (Microsoft.Build.Framework), reads it directly.

    // The test host sets the field by reflection: TestAssemblyInfo is compiled into every test
    //  assembly, and reflection lets that one shared file set the flag without requiring an
    //  InternalsVisibleTo entry from Microsoft.Build.Framework for each of those assemblies.
    internal static class TestInfo
    {
        public static bool s_runningTests = false;
    }
}
