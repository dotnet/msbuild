// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Tools.Test.Utilities;

namespace Microsoft.DotNet.Restore.Test
{
    public class GivenThatIWantToRestoreApp : SdkTest
    {
        public GivenThatIWantToRestoreApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CreateTestDirectory(identifier: useStaticGraphEvaluation.ToString()).Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            var sln = "TestAppWithSlnAndSolutionFolders";
            var projectDirectory = _testAssetsManager
                .CopyTestAsset(sln, identifier: useStaticGraphEvaluation.ToString())
                .WithSource()
                .Path;

            string[] args = new[] { "--packages", fullPath };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(projectDirectory)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true, ".csproj")]
        [InlineData(false, ".csproj")]
        [InlineData(true, ".fsproj")]
        [InlineData(false, ".fsproj")]
        public void ItRestoresLibToSpecificDirectory(bool useStaticGraphEvaluation, string extension)
        {
            var testProject = new TestProject()
            {
                Name = "RestoreToDir",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            if (extension == ".fsproj")
            {
                testProject.PackageReferences.Add(new TestPackageReference("FSharp.Core", "6.0.1", updatePackageReference: true));
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: useStaticGraphEvaluation.ToString() + extension, targetExtension: extension);

            var rootPath = Path.Combine(testAsset.TestRoot, testProject.Name);

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            var dllCount = 0;

            if (Directory.Exists(fullPath))
            {
                dllCount = Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count();
            }

            if (dllCount == 0)
            {
                Log.WriteLine("Assets file contents:");
                Log.WriteLine(File.ReadAllText(Path.Combine(rootPath, "obj", "project.assets.json")));
            }

            Directory.Exists(fullPath).Should().BeTrue();
            dllCount.Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresTestAppToSpecificDirectory(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CopyTestAsset("VSTestCore", identifier: useStaticGraphEvaluation.ToString())
                .WithSource()
                .WithVersionVariables()
                .Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] args = new[] { "--packages", dir };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            Directory.Exists(fullPath).Should().BeTrue();
            Directory.EnumerateFiles(fullPath, "*.dll", SearchOption.AllDirectories).Count().Should().BeGreaterThan(0);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItRestoresWithTheSpecifiedVerbosity(bool useStaticGraphEvaluation)
        {
            var rootPath = _testAssetsManager.CreateTestDirectory(identifier: useStaticGraphEvaluation.ToString()).Path;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string[] newArgs = new[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "--packages", dir, "--verbosity",  "quiet" };
            args = HandleStaticGraphEvaluation(useStaticGraphEvaluation, args);
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass()
                 .And.NotHaveStdErr()
                 .And.NotHaveStdOut();
        }

        [Fact]
        public void ItAcceptsArgumentsAfterProperties()
        {
            var rootPath = _testAssetsManager.CreateTestDirectory().Path;

            string[] newArgs = new[] { "console", "-o", rootPath, "--no-restore" };
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            string[] args = new[] { "/p:prop1=true", "/m:1" };
            new DotnetRestoreCommand(Log)
                 .WithWorkingDirectory(rootPath)
                 .Execute(args)
                 .Should()
                 .Pass();
        }

        private static string[] HandleStaticGraphEvaluation(bool useStaticGraphEvaluation, string[] args) =>
            useStaticGraphEvaluation ? 
                args.Append("/p:RestoreUseStaticGraphEvaluation=true").ToArray() :
                args;
    }
}
