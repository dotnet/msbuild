// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Packaging;

namespace Microsoft.NET.ToolPack.Tests
{
    public class GivenThatWeWantToPackAToolProjectWithGeneratePackageOnBuild : SdkTest
    {

        private const string AppName = "consoledemo";

        public GivenThatWeWantToPackAToolProjectWithGeneratePackageOnBuild(ITestOutputHelper log) : base(log)
        {}

        private TestAsset SetupAndRestoreTestAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("PortableToolWithP2P", callingMethod)
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    if (IsAppProject(projectPath))
                    {
                        XNamespace ns = project.Root.Name.Namespace;
                        XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Add(new XElement(ns + "GeneratePackageOnBuild", "true"));
                    }
                });

            return testAsset;
        }

        [Fact]
        public void It_builds_successfully()
        {
            TestAsset testAsset = SetupAndRestoreTestAsset();
            var buildCommand = new BuildCommand(testAsset, "App");

            CommandResult result = buildCommand.Execute();

            result.Should()
                  .Pass()
                  .And
                  .NotHaveStdOutContaining("There is a circular dependency");
        }

        [Fact]
        public void It_builds_and_result_contains_dependencies_dll()
        {
            TestAsset testAsset = SetupAndRestoreTestAsset();
            var buildCommand = new BuildCommand(testAsset, "App");
            buildCommand.Execute();

            var packCommand = new PackCommand(testAsset, "App");
            // Do not run pack, just use it to get nupkg since it should be run by build.
            var nugetPackage = packCommand.GetNuGetPackage();

            using(var nupkgReader = new PackageArchiveReader(nugetPackage))
            {
                IEnumerable<NuGet.Frameworks.NuGetFramework> supportedFrameworks = nupkgReader.GetSupportedFrameworks();
                supportedFrameworks.Should().NotBeEmpty();

                foreach (NuGet.Frameworks.NuGetFramework framework in supportedFrameworks)
                {
                    var allItems = nupkgReader.GetToolItems().SelectMany(i => i.Items).ToList();
                    allItems.Should().Contain($"tools/{framework.GetShortFolderName()}/any/Library.dll");
                }
            }
        }

        [Theory(Skip = "https://github.com/dotnet/sdk/issues/3471")]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void It_packs_successfully(bool generatePackageOnBuild, bool packAsTool)
        {
            Console.WriteLine(generatePackageOnBuild.ToString() + packAsTool.ToString());

            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: generatePackageOnBuild.ToString() + packAsTool.ToString())
                .WithSource()
                .WithProjectChanges((projectPath, project) =>
                {
                    XNamespace ns = project.Root.Name.Namespace;
                    XElement propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement(ns + "GeneratePackageOnBuild", generatePackageOnBuild.ToString()));
                    propertyGroup.Add(new XElement(ns + "PackAsTool", packAsTool.ToString()));
                });

            var appProjectDirectory = Path.Combine(testAsset.TestRoot);
            var packCommand = new PackCommand(Log, appProjectDirectory);

            CommandResult result = packCommand.Execute("/restore");

            result.Should()
                  .Pass();
        }

        private bool IsAppProject(string projectPath)
        {
            return Path.GetFileNameWithoutExtension(projectPath).Equals(AppName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
