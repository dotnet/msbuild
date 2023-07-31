// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class PackCommand : MSBuildCommand
    {
        public PackCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "Pack", projectPath, relativePathToProject)
        {

        }

        public PackCommand(TestAsset testAsset, string relativePathToProject = null)
            : base(testAsset, "Pack", relativePathToProject)
        {

        }

        public string GetIntermediateNuspecPath(string packageId = null, string configuration = "Debug", string packageVersion = "1.0.0")
        {
            if (packageId == null)
            {
                packageId = Path.GetFileNameWithoutExtension(ProjectFile);
            }

            return Path.Combine(GetBaseIntermediateDirectory().FullName, configuration, $"{packageId}.{packageVersion}.nuspec");
        }

        public string GetNuGetPackage(string packageId = null, string configuration = "Debug", string packageVersion = "1.0.0")
        {
            if (packageId == null)
            {
                packageId = Path.GetFileNameWithoutExtension(ProjectFile);
            }

            return Path.Combine(GetPackageDirectory(configuration).FullName, $"{packageId}.{packageVersion}.nupkg");
        }
    }
}
