using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.NET.TestFramework
{
    public class TestContext
    {
        //  Generally the folder the test DLL is in
        public string TestExecutionDirectory { get; set; }

        public string TestAssetsDirectory { get; set; }

        public string NuGetCachePath { get; set; }

        public string NuGetFallbackFolder { get; set; }

        // For test purposes, override the implicit .NETCoreApp version for self-contained apps that to builds thare 
        //  (1) different from the fixed framework-dependent defaults (1.0.5, 1.1.2, 2.0.0)
        //  (2) currently available on nuget.org
        //
        // This allows bumping the versions before builds without causing tests to fail.
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0 = "1.0.4";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1 = "1.1.1";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0 = "2.0.0-preview2-25407-01";

        public ToolsetInfo ToolsetUnderTest
        {
            get
            {
                return RepoInfo.ToolsetUnderTest;
            }
        }

        public static TestContext Current
        {
            get
            {
                return RepoInfo.TestExecutionInfo;
            }
        }

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            command.Environment["NUGET_PACKAGES"] = NuGetCachePath;

            command.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0;
            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1;
            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0;

            ToolsetUnderTest.AddTestEnvironmentVariables(command);
        }
    }
}
