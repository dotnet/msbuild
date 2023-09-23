// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Restore.Tests
{
    public class GivenThatWeWantToIgnoreObsoleteDotNetCliToolPackages : SdkTest
    {
        public GivenThatWeWantToIgnoreObsoleteDotNetCliToolPackages(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_issues_warning_and_skips_restore_for_obsolete_DotNetCliToolReference()
        {
            const string obsoletePackageId = "Banana.CommandLineTool";

            TestProject toolProject = new()
            {
                Name = "ObsoleteCliToolRefRestoreProject",
                TargetFrameworks = "netstandard2.0",
            };

            toolProject.DotNetCliToolReferences.Add(new TestPackageReference(obsoletePackageId, "99.99.99", null));

            TestAsset toolProjectInstance = _testAssetsManager.CreateTestProject(toolProject, identifier: toolProject.Name)
                .WithProjectChanges(project =>
                {
                    XNamespace ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "BundledDotNetCliToolReference",
                        new XAttribute("Include", obsoletePackageId)));
                });

            NuGetConfigWriter.Write(toolProjectInstance.TestRoot, NuGetConfigWriter.DotnetCoreBlobFeed);

            RestoreCommand restoreCommand = toolProjectInstance.GetRestoreCommand(Log, toolProject.Name);
            restoreCommand.Execute("/v:n").Should()
                .Pass()
                .And
                .HaveStdOutContaining($"warning NETSDK1059: The tool '{obsoletePackageId}' is now included in the .NET SDK. Information on resolving this warning is available at (https://aka.ms/dotnetclitools-in-box).");

            string toolAssetsFilePath = Path.Combine(TestContext.Current.NuGetCachePath, ".tools", toolProject.Name.ToLowerInvariant(), "99.99.99", toolProject.TargetFrameworks, "project.assets.json");
            Assert.False(File.Exists(toolAssetsFilePath), "Tool assets path should not have been generated");
        }
    }
}
