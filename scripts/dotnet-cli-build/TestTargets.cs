using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;

using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.Utils;

namespace Microsoft.DotNet.Cli.Build
{
    public class TestTargets
    {
        private static string s_testPackageBuildVersionSuffix = "<buildversion>";

        public static readonly string[] TestProjects = new[]
        {
            "EndToEnd",
            "dotnet.Tests",
            "dotnet-publish.Tests",
            "dotnet-compile.Tests",
            "dotnet-compile.UnitTests",
            "dotnet-compile-fsc.Tests",
            "dotnet-build.Tests",
            "dotnet-pack.Tests",
            "dotnet-projectmodel-server.Tests",
            "dotnet-resgen.Tests",
            "dotnet-run.Tests",
            "Microsoft.DotNet.Cli.Utils.Tests",
            "Microsoft.DotNet.Compiler.Common.Tests",
            "Microsoft.DotNet.ProjectModel.Tests",
            "Microsoft.Extensions.DependencyModel.Tests",
            "ArgumentForwardingTests",
            "dotnet-test.UnitTests",
            "dotnet-test.Tests"
        };
        
        public static readonly dynamic[] ConditionalTestAssets = new[]
        {
            new { Path = "AppWithDirectDependencyDesktopAndPortable", Skip = new Func<bool>(() => !CurrentPlatform.IsWindows) } 
        };

        [Target(
            nameof(PrepareTargets.Init), 
            nameof(SetupTests), 
            nameof(RestoreTests), 
            nameof(BuildTests), 
            nameof(RunTests),
            nameof(ValidateDependencies))]
        public static BuildTargetResult Test(BuildTargetContext c) => c.Success();

        [Target(nameof(SetupTestPackages), nameof(SetupTestProjects))]
        public static BuildTargetResult SetupTests(BuildTargetContext c) => c.Success();

        [Target(nameof(RestoreTestAssetPackages), nameof(BuildTestAssetPackages))]
        public static BuildTargetResult SetupTestPackages(BuildTargetContext c) => c.Success();

        [Target(nameof(RestoreTestAssetProjects), nameof(RestoreDesktopTestAssetProjects), nameof(BuildTestAssetProjects))]
        public static BuildTargetResult SetupTestProjects(BuildTargetContext c) => c.Success();

        [Target]
        public static BuildTargetResult RestoreTestAssetPackages(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();

            var dotnet = DotNetCli.Stage2;
            dotnet.Restore("--verbosity", "verbose", "--infer-runtimes", "--disable-parallel")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestPackages"))
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        [Target]
        public static BuildTargetResult RestoreTestAssetProjects(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();

            var dotnet = DotNetCli.Stage2;
            dotnet.Restore(
                "--verbosity", "verbose", 
                "--disable-parallel", 
                "--infer-runtimes",
                "--fallbacksource", Dirs.TestPackages)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects"))
                .Execute().EnsureSuccessful();

            // The 'ProjectModelServer' directory contains intentionally-unresolved dependencies, so don't check for success. Also, suppress the output
            dotnet.Restore(
                "--verbosity", "verbose", 
                "--disable-parallel", 
                "--infer-runtimes")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "ProjectModelServer", "DthTestProjects"))
                .Execute();

            dotnet.Restore(
                "--verbosity", "verbose", 
                "--disable-parallel", 
                "--infer-runtimes")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "ProjectModelServer", "DthUpdateSearchPathSample"))
                .Execute();

            return c.Success();
        }

        [Target]
        [BuildPlatforms(BuildPlatform.Windows)]
        public static BuildTargetResult RestoreDesktopTestAssetProjects(BuildTargetContext c)
        {
            var dotnet = DotNetCli.Stage2;

            dotnet.Restore("--verbosity", "verbose", 
                "--disable-parallel", "--infer-runtimes",
                "--fallbacksource", Dirs.TestPackages)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "DesktopTestProjects"))
                .Execute().EnsureSuccessful();
                
            return c.Success();
        }

        [Target(nameof(CleanTestPackages), nameof(CleanProductPackages))]
        public static BuildTargetResult BuildTestAssetPackages(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestPackages"));

            var dotnet = DotNetCli.Stage2;

            Rmdir(Dirs.TestPackages);
            Mkdirp(Dirs.TestPackages);

            foreach (var testPackageProject in TestPackageProjects.Projects.Where(p => p.IsApplicable()))
            {
                var relativePath = testPackageProject.Path;

                var versionSuffix = testPackageProject.VersionSuffix;
                if (versionSuffix.Equals(s_testPackageBuildVersionSuffix))
                {
                    versionSuffix = c.BuildContext.Get<BuildVersion>("BuildVersion").VersionSuffix;
                }
                
                var fullPath = Path.Combine(c.BuildContext.BuildDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                c.Info($"Packing: {fullPath}");

                // build and ignore failure, so net451 fail on non-windows doesn't crash the build
                var packageBuildFrameworks = new List<string>()
                {
                    "netstandard1.5",
                    "netstandard1.3",
                    "netstandardapp1.5"
                };

                if (CurrentPlatform.IsWindows)
                {
                    packageBuildFrameworks.Add("net451");
                }

                foreach (var packageBuildFramework in packageBuildFrameworks)
                {
                    var buildArgs = new List<string>();
                    buildArgs.Add("-f");
                    buildArgs.Add(packageBuildFramework);
                    buildArgs.Add("--build-base-path");
                    buildArgs.Add(Dirs.TestPackagesBuild);
                    buildArgs.Add(fullPath);

                    Mkdirp(Dirs.TestPackagesBuild);
                    var packBuildResult = DotNetCli.Stage1.Build(buildArgs.ToArray())
                        .Execute();
                }
                

                var projectJson = Path.Combine(fullPath, "project.json");
                var dotnetPackArgs = new List<string> { 
                    projectJson,
                    "--no-build",
                    "--build-base-path", Dirs.TestPackagesBuild,
                    "--output", Dirs.TestPackages 
                };

                if (!string.IsNullOrEmpty(versionSuffix))
                {
                    dotnetPackArgs.Add("--version-suffix");
                    dotnetPackArgs.Add(versionSuffix);
                }

                dotnet.Pack(dotnetPackArgs.ToArray())
                    .Execute()
                    .EnsureSuccessful();
            }

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CleanProductPackages(BuildTargetContext c)
        {
            foreach (var packageName in PackageTargets.ProjectsToPack)
            {
                Rmdir(Path.Combine(Dirs.NuGetPackages, packageName));
            }            

            return c.Success();
        }

        [Target]
        public static BuildTargetResult CleanTestPackages(BuildTargetContext c)
        {
            foreach (var packageProject in TestPackageProjects.Projects.Where(p => p.IsApplicable() && p.Clean))
            {
                Rmdir(Path.Combine(Dirs.NuGetPackages, packageProject.Name));
                if(packageProject.IsTool)
                {
                    Rmdir(Path.Combine(Dirs.NuGetPackages, ".tools", packageProject.Name));
                }
            }            

            return c.Success();
        }

        [Target]
        public static BuildTargetResult BuildTestAssetProjects(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects"));

            var dotnet = DotNetCli.Stage2;
            var nobuildFileName = ".noautobuild";
            string testProjectsRoot = Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects");
            var projects = Directory.GetFiles(testProjectsRoot, "project.json", SearchOption.AllDirectories)
                                    .Where(p => !ConditionalTestAssets.Where(s => !s.Skip() && p.EndsWith(Path.Combine(s.Path, "project.json"))).Any())
                                    .Where(p => !File.Exists(Path.Combine(Path.GetDirectoryName(p), nobuildFileName)));

            foreach (var project in projects)
            {
                c.Info($"Building: {project}");
                dotnet.Build("--framework", "netstandardapp1.5")
                    .WorkingDirectory(Path.GetDirectoryName(project))
                    .Execute()
                    .EnsureSuccessful();
            }

            return c.Success();
        }

        [Target]
        public static BuildTargetResult RestoreTests(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();
            DotNetCli.Stage2.Restore("--verbosity", "verbose", "--disable-parallel", "--infer-runtimes","--fallbacksource", Dirs.TestPackages)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "test"))
                .Execute()
                .EnsureSuccessful();
            return c.Success();
        }

        [Target]
        public static BuildTargetResult BuildTests(BuildTargetContext c)
        {
            var dotnet = DotNetCli.Stage2;

            var configuration = c.BuildContext.Get<string>("Configuration");

            foreach (var testProject in TestProjects)
            {
                c.Info($"Building tests: {testProject}");
                dotnet.Build("--configuration", configuration)
                    .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "test", testProject))
                    .Execute()
                    .EnsureSuccessful();
            }
            return c.Success();
        }

        [Target(nameof(RunXUnitTests))]
        public static BuildTargetResult RunTests(BuildTargetContext c) => c.Success();

        [Target]
        public static BuildTargetResult RunXUnitTests(BuildTargetContext c)
        {
            // Need to load up the VS Vars
            var dotnet = DotNetCli.Stage2;
            var vsvars = LoadVsVars(c);

            var configuration = c.BuildContext.Get<string>("Configuration");

            // Copy the test projects
            var testProjectsDir = Path.Combine(Dirs.TestOutput, "TestProjects");
            Rmdir(testProjectsDir);
            Mkdirp(testProjectsDir);
            CopyRecursive(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects"), testProjectsDir);

            // Run the tests and set the VS vars in the environment when running them
            var failingTests = new List<string>();
            foreach (var project in TestProjects)
            {
                c.Info($"Running tests in: {project}");
                var result = dotnet.Test("--configuration", configuration, "-xml", $"{project}-testResults.xml", "-notrait", "category=failing")
                    .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "test", project))
                    .Environment(vsvars)
                    .EnvironmentVariable("PATH", $"{DotNetCli.Stage2.BinPath}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}")
                    .EnvironmentVariable("TEST_ARTIFACTS", Dirs.TestArtifacts)
                    .Execute();
                if (result.ExitCode != 0)
                {
                    failingTests.Add(project);
                }
            }

            if (failingTests.Any())
            {
                foreach (var project in failingTests)
                {
                    c.Error($"{project} failed");
                }
                return c.Failed("Tests failed!");
            }

            return c.Success();
        }

        [Target]
        public static BuildTargetResult ValidateDependencies(BuildTargetContext c)
        {
            var configuration = c.BuildContext.Get<string>("Configuration");
            var dotnet = DotNetCli.Stage2;

            c.Info("Publishing MultiProjectValidator");
            dotnet.Publish("--output", Path.Combine(Dirs.Output, "tools"), "--configuration", configuration)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "tools", "MultiProjectValidator"))
                .Execute()
                .EnsureSuccessful();

            var validator = Path.Combine(Dirs.Output, "tools", $"pjvalidate{Constants.ExeSuffix}");

            Cmd(validator, Path.Combine(c.BuildContext.BuildDirectory, "src"))
                .Execute();

            return c.Success();
        }

        private static Dictionary<string, string> LoadVsVars(BuildTargetContext c)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new Dictionary<string, string>();
            }

            c.Verbose("Start Collecting Visual Studio Environment Variables");

            var vsvarsPath = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("VS140COMNTOOLS"), "..", "..", "VC"));

            // Write a temp batch file because that seems to be the easiest way to do this (argument parsing is hard)
            var temp = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.cmd");
            File.WriteAllText(temp, $@"@echo off
cd {vsvarsPath}
call vcvarsall.bat x64
set");

            CommandResult result;
            try
            {
                result = Cmd(Environment.GetEnvironmentVariable("COMSPEC"), "/c", temp)
                    .WorkingDirectory(vsvarsPath)
                    .CaptureStdOut()
                    .Execute();
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }

            result.EnsureSuccessful();

            var vars = new Dictionary<string, string>();
            foreach (var line in result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                var splat = line.Split(new[] { '=' }, 2);

                if (splat.Length == 2)
                {
                    c.Verbose($"Adding variable '{line}'");
                    vars[splat[0]] = splat[1];
                }
                else
                {
                    c.Info($"Skipping VS Env Variable. Unknown format: '{line}'");
                }
            }

            c.Verbose("Finish Collecting Visual Studio Environment Variables");
            return vars;
        }
    }
}
