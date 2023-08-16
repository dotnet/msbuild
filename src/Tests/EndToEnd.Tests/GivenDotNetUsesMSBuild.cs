// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class GivenDotNetUsesMSBuild : SdkTest
    {
        public GivenDotNetUsesMSBuild(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ItCanNewRestoreBuildRunCleanMSBuildProject()
        {
            string projectDirectory = _testAssetsManager.CreateTestDirectory().Path;

            string[] newArgs = new[] { "console", "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(projectDirectory)
                .Execute(newArgs)
                .Should().Pass();

            new BuildCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectDirectory)
                .Execute()
                .Should().Pass()
                        .And.HaveStdOutContaining("Hello, World!");

            var binDirectory = new DirectoryInfo(projectDirectory).Sub("bin");
            binDirectory.Should().HaveFilesMatching("*.dll", SearchOption.AllDirectories);

            new CleanCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            binDirectory.Should().NotHaveFilesMatching("*.dll", SearchOption.AllDirectories);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void ItCanRunToolsInACSProj()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                                         .WithSource()
                                         .WithProjectChanges(project =>
                                         {
                                             var ns = project.Root.Name.Namespace;

                                             var itemGroup = new XElement(ns + "ItemGroup");
                                             itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                                                                new XAttribute("Include", "dotnet-portable"),
                                                                new XAttribute("Version", "1.0.0")));

                                             project.Root.Add(itemGroup);
                                         });

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("portable")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello Portable World!"); ;
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void ItCanRunToolsThatPrefersTheCliRuntimeEvenWhenTheToolItselfDeclaresADifferentRuntime()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                                         .WithSource()
                                         .WithProjectChanges(project =>
                                         {
                                             var ns = project.Root.Name.Namespace;

                                             var itemGroup = new XElement(ns + "ItemGroup");
                                             itemGroup.Add(new XElement(ns + "DotNetCliToolReference",
                                                                new XAttribute("Include", "dotnet-prefercliruntime"),
                                                                new XAttribute("Version", "1.0.0")));

                                             project.Root.Add(itemGroup);
                                         });
            ;

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            var testProjectDirectory = testInstance.Path;

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("prefercliruntime")
                .Should().Pass()
                .And.HaveStdOutContaining("Hello I prefer the cli runtime World!"); ;
        }
    }
}
