// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    [Trait("AspNetCore", "Integration")]
    public abstract class AspNetSdkTest : SdkTest
    {
        public readonly string DefaultTfm;

        protected AspNetSdkTest(ITestOutputHelper log) : base(log)
        {
            var assembly = Assembly.GetCallingAssembly();
            var testAssemblyMetadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            DefaultTfm = testAssemblyMetadata.SingleOrDefault(a => a.Key == "AspNetTestTfm").Value;
        }

        public TestAsset CreateAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null,
            string identifier = null)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory, identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .SingleOrDefault(e => e.Name.LocalName == "TargetFramework");
                    if (targetFramework?.Value == "$(AspNetTestTfm)")
                    {
                        targetFramework.Value = overrideTfm ?? DefaultTfm;
                    }
                    var targetFrameworks = project.Descendants()
                        .SingleOrDefault(e => e.Name.LocalName == "TargetFrameworks");
                    if (targetFrameworks != null)
                    {
                        targetFrameworks.Value = targetFrameworks.Value.Replace("$(AspNetTestTfm)", overrideTfm ?? DefaultTfm);
                    }
                });

            foreach (string assetPath in Directory.EnumerateFiles(Path.Combine(_testAssetsManager.TestAssetsRoot, "WasmOverride")))
                File.Copy(assetPath, Path.Combine(projectDirectory.Path, Path.GetFileName(assetPath)));

            return projectDirectory;
        }

        public TestAsset CreateMultitargetAspNetSdkTestAsset(
            string testAsset,
            [CallerMemberName] string callerName = "",
            string subdirectory = "",
            string overrideTfm = null,
            string identifier = null)
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(testAsset, callingMethod: callerName, testAssetSubdirectory: subdirectory, identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var targetFramework = project.Descendants()
                       .Single(e => e.Name.LocalName == "TargetFrameworks");
                    targetFramework.Value = targetFramework.Value.Replace("$(AspNetTestTfm)", overrideTfm ?? DefaultTfm);
                });
            return projectDirectory;
        }
    }
}
