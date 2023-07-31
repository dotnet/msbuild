// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class ToolsetUtils
{
    /// <summary>
    /// Gets path to RuntimeIdentifierGraph.json file.
    /// </summary>
    /// <returns></returns>
    internal static string GetRuntimeGraphFilePath()
    {
        string dotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;

        DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

        var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

        return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
    }

    /// <summary>
    /// Gets path to built Microsoft.NET.Build.Containers.*.nupkg prepared for tests.
    /// </summary>
    /// <returns></returns>
    internal static (string PackagePath, string PackageVersion) GetContainersPackagePath()
    {
        string packageDir = Path.Combine(TestContext.Current.TestExecutionDirectory, "Container", "package");

        //until the package is stabilized, the package version matches TestContext.Current.ToolsetUnderTest.SdkVersion
        //after the package is stabilized, the package version doesn't have -prefix (-dev, -ci) anymore
        //so one of those is expected
        string[] expectedPackageVersions = new[] { TestContext.Current.ToolsetUnderTest.SdkVersion, TestContext.Current.ToolsetUnderTest.SdkVersion.Split('-')[0] };

        foreach (string expectedVersion in expectedPackageVersions)
        {
            string fullFileName = Path.Combine(packageDir, $"Microsoft.NET.Build.Containers.{expectedVersion}.nupkg");
            if (File.Exists(fullFileName))
            {
                return (fullFileName, expectedVersion);
            }
        }

        throw new FileNotFoundException($"No Microsoft.NET.Build.Containers.*.nupkg found in expected package folder {packageDir}. Tried the following package versions: {string.Join(", ", expectedPackageVersions.Select(v => $"'Microsoft.NET.Build.Containers.{v}.nupkg'"))}. You may need to rerun the build.");
    }
}
