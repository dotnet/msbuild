using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;

using static Microsoft.DotNet.Cli.Build.FS;
using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using static Microsoft.DotNet.Cli.Build.Utils;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class TestTargets : Task
    {
        private static string s_testPackageBuildVersionSuffix = "<buildversion>";

        public static readonly string[] TestProjects = new[]
        {
            "ArgumentForwardingTests",
            "crossgen.Tests",
            "EndToEnd",
            "dotnet.Tests",
            "dotnet-build.Tests",
            "dotnet-build3.Tests",
            "dotnet-compile.Tests",
            "dotnet-compile.UnitTests",
            // TODO: https://github.com/dotnet/cli/issues/3558
            // "dotnet-compile-fsc.Tests",
            "dotnet-new.Tests",
            "dotnet-pack.Tests",
            "dotnet-publish.Tests",
            "dotnet-resgen.Tests",
            "dotnet-run.Tests",
            "dotnet-run.UnitTests",
            "dotnet-test.Tests",
            "dotnet-test.UnitTests",
            // TODO: https://github.com/dotnet/cli/issues/3216
            //"Kestrel.Tests",
            "Microsoft.DotNet.Cli.Utils.Tests",
            "Microsoft.DotNet.Compiler.Common.Tests",
            "Microsoft.DotNet.ProjectModel.Tests",
            "Microsoft.DotNet.ProjectModel.Loader.Tests",
            "Microsoft.Extensions.DependencyModel.Tests",
            "Microsoft.DotNet.Configurer.UnitTests",
            "Performance"
        };

        public static readonly string[] WindowsTestProjects = new[]
        {
            "binding-redirects.Tests"
        };

        public static readonly dynamic[] ConditionalTestAssets = new[]
        {
            new { Path = "AppWithDirectDependencyDesktopAndPortable", Skip = new Func<bool>(() => !CurrentPlatform.IsWindows) }
        };

        public override bool Execute()
        {
            BuildContext context = new BuildSetup("MSBuild").UseAllTargetsFromAssembly<TestTargets>().CreateBuildContext();
            BuildTargetContext c = new BuildTargetContext(context, null, null);

            return Test(c).Success;
        }

        [Target]
        public static BuildTargetResult Test(BuildTargetContext c)
        {
            PrepareTargets.Init(c);
            SetupTests(c);
            RestoreTests(c);
            BuildTests(c);
            RunTests(c);

            return c.Success();
        }

        public static BuildTargetResult SetupTests(BuildTargetContext c)
        {
            SetupTestPackages(c);
            SetupTestProjects(c);

            return c.Success();
        }

        public static BuildTargetResult SetupTestPackages(BuildTargetContext c)
        {
            RestoreTestAssetPackages(c);
            BuildTestAssetPackages(c);

            return c.Success();
        }

        public static BuildTargetResult SetupTestProjects(BuildTargetContext c)
        {
            RestoreTestAssetProjects(c);
            RestoreDesktopTestAssetProjects(c);
            BuildTestAssetProjects(c);
            BuildDesktopTestAssetProjects(c);

            return c.Success();
        }

        public static BuildTargetResult RestoreTestAssetPackages(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();

            var dotnet = DotNetCli.Stage2;
            dotnet.Restore("--verbosity", "verbose")
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestPackages"))
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        public static BuildTargetResult RestoreTestAssetProjects(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();

            var dotnet = DotNetCli.Stage2;
            dotnet.Restore(
                "--verbosity", "verbose",
                "--fallbacksource", Dirs.TestPackages)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects"))
                .Execute()
                .EnsureSuccessful();

            return c.Success();
        }

        public static BuildTargetResult RestoreDesktopTestAssetProjects(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                var dotnet = DotNetCli.Stage2;

                dotnet.Restore("--verbosity", "verbose",
                    "--infer-runtimes",
                    "--fallbacksource", Dirs.TestPackages)
                    .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "DesktopTestProjects"))
                    .Execute().EnsureSuccessful();
            }

            return c.Success();
        }

        public static BuildTargetResult BuildTestAssetPackages(BuildTargetContext c)
        {
            CleanTestPackages(c);
            CleanProductPackages(c);

            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestPackages"));

            var dotnet = DotNetCli.Stage2;

            Rmdir(Dirs.TestPackages);
            Mkdirp(Dirs.TestPackages);

            foreach (var testPackageProject in TestPackageProjects.Projects.Where(p => p.IsApplicable))
            {
                var relativePath = testPackageProject.Path;

                var versionSuffix = testPackageProject.VersionSuffix;
                if (versionSuffix.Equals(s_testPackageBuildVersionSuffix))
                {
                    versionSuffix = c.BuildContext.Get<BuildVersion>("BuildVersion").CommitCountString;
                }

                var fullPath = Path.Combine(c.BuildContext.BuildDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
                c.Info($"Packing: {fullPath}");

                var packageBuildFrameworks = testPackageProject.Frameworks.ToList();

                if (!CurrentPlatform.IsWindows)
                {
                    packageBuildFrameworks.RemoveAll(f => f.StartsWith("net4"));
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
                    dotnet.Build(buildArgs.ToArray())
                        .Execute()
                        .EnsureSuccessful();
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

        public static BuildTargetResult CleanProductPackages(BuildTargetContext c)
        {
            foreach (var packageName in PackageTargets.ProjectsToPack)
            {
                Rmdir(Path.Combine(Dirs.NuGetPackages, packageName));
            }

            return c.Success();
        }

        public static BuildTargetResult CleanTestPackages(BuildTargetContext c)
        {
            foreach (var packageProject in TestPackageProjects.Projects.Where(p => p.IsApplicable && p.Clean))
            {
                Rmdir(Path.Combine(Dirs.NuGetPackages, packageProject.Name));
                if (packageProject.IsTool)
                {
                    Rmdir(Path.Combine(Dirs.NuGetPackages, ".tools", packageProject.Name));
                }
            }
            return c.Success();
        }

        public static BuildTargetResult BuildTestAssetProjects(BuildTargetContext c)
        {
            var testAssetsRoot = Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "TestProjects");
            var dotnet = DotNetCli.Stage2;
            var framework = "netcoreapp1.0";

            return BuildTestAssets(c, testAssetsRoot, dotnet, framework);
        }

        public static BuildTargetResult BuildDesktopTestAssetProjects(BuildTargetContext c)
        {
            if (CurrentPlatform.IsPlatform(BuildPlatform.Windows))
            {
                var testAssetsRoot = Path.Combine(c.BuildContext.BuildDirectory, "TestAssets", "DesktopTestProjects");
                var dotnet = DotNetCli.Stage2;
                var framework = "net451";

                return BuildTestAssets(c, testAssetsRoot, dotnet, framework);
            }

            return c.Success();
        }

        public static BuildTargetResult RestoreTests(BuildTargetContext c)
        {
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "src"));
            CleanBinObj(c, Path.Combine(c.BuildContext.BuildDirectory, "test"));

            CleanNuGetTempCache();
            DotNetCli.Stage2.Restore("--verbosity", "verbose",
                "--fallbacksource", Dirs.TestPackages)
                .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "test"))
                .Execute()
                .EnsureSuccessful();
            return c.Success();
        }

        public static BuildTargetResult BuildTests(BuildTargetContext c)
        {
            var dotnet = DotNetCli.Stage2;

            var configuration = c.BuildContext.Get<string>("Configuration");

            foreach (var testProject in GetTestProjects())
            {
                c.Info($"Building tests: {testProject}");
                dotnet.Build("--configuration", configuration)
                    .WorkingDirectory(Path.Combine(c.BuildContext.BuildDirectory, "test", testProject))
                    .Execute()
                    .EnsureSuccessful();
            }
            return c.Success();
        }

        public static BuildTargetResult RunTests(BuildTargetContext c)
        {
            RunXUnitTests(c);

            return c.Success();
        }

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
            foreach (var project in GetTestProjects())
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

        private static IEnumerable<string> GetTestProjects()
        {
            List<string> testProjects = new List<string>();
            testProjects.AddRange(TestProjects);

            if (CurrentPlatform.IsWindows)
            {
                testProjects.AddRange(WindowsTestProjects);
            }

            return testProjects;
        }

        private static BuildTargetResult BuildTestAssets(BuildTargetContext c, string testAssetsRoot, DotNetCli dotnet, string framework)
        {
            CleanBinObj(c, testAssetsRoot);

            var nobuildFileName = ".noautobuild";

            var projects = Directory.GetFiles(testAssetsRoot, "project.json", SearchOption.AllDirectories)
                                    .Where(p => !ConditionalTestAssets.Where(s => !s.Skip() && p.EndsWith(Path.Combine(s.Path, "project.json"))).Any())
                                    .Where(p => !File.Exists(Path.Combine(Path.GetDirectoryName(p), nobuildFileName)));

            foreach (var project in projects)
            {
                c.Info($"Building: {project}");
                dotnet.Build("--framework", framework)
                    .WorkingDirectory(Path.GetDirectoryName(project))
                    .Execute()
                    .EnsureSuccessful();
            }

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
