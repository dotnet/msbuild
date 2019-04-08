using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.Xml.Linq;
using System.Reflection;
using Xunit.Abstractions;
using Microsoft.Build.Utilities;
using NuGet.Versioning;

namespace Microsoft.NET.TestFramework
{
    public class ToolsetInfo
    {
        public string DotNetHostPath { get; set; }

        public string SdksPath { get; set; }

        public string GetMicrosoftNETBuildExtensionsPath(ITestOutputHelper log)
        {
            if (!string.IsNullOrEmpty(SdksPath))
            {
                var buildExtensionsSdkPath = Path.Combine(SdksPath, "Microsoft.NET.Build.Extensions");
                return Path.Combine(buildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions");
            }
            else
            {
                var msbuildBinPath = Path.GetDirectoryName(FullFrameworkMSBuildPath);
                if (ShouldUseFullFrameworkMSBuild)
                {
                    var msbuildRoot = Directory.GetParent(msbuildBinPath).Parent.FullName;
                    return Path.Combine(msbuildRoot, @"Microsoft\Microsoft.NET.Build.Extensions");
                }
                else
                {
                    var dotnetSdkDir = GetDotnetSdkDir(log);
                    return Path.Combine(dotnetSdkDir, @"Microsoft\Microsoft.NET.Build.Extensions");
                }
            }
        }

        public bool ShouldUseFullFrameworkMSBuild => !string.IsNullOrEmpty(FullFrameworkMSBuildPath);

        public string FullFrameworkMSBuildPath { get; set; }

        public string GetDotnetSdkDir(ITestOutputHelper log)
        {
            var command = new DotnetCommand(log, "--version");
            var testDirectory = TestDirectory.Create(Path.Combine(TestContext.Current.TestExecutionDirectory, "sdkversion"));

            command.WorkingDirectory = testDirectory.Path;

            var result = command.Execute();

            result.Should().Pass();

            var sdkVersion = result.StdOut.Trim();
            string dotnetDir = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);
            return Path.Combine(dotnetDir, "sdk", sdkVersion);
        }
        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            if (SdksPath != null)
            {
                command.Environment["MSBuildSDKsPath"] = SdksPath;
                command.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = SdksPath;

                //  OK to pass in null as the logger here because SdksPath is set so it won't go down the code path
                //  that uses the logger
                var microsoftNETBuildExtensionsPath = GetMicrosoftNETBuildExtensionsPath(null);
                command.Environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(microsoftNETBuildExtensionsPath, "Microsoft.NET.Build.Extensions.targets");

                if (UsingFullMSBuildWithoutExtensionsTargets())
                {
                    command.Environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(SdksPath, "Microsoft.NET.Build.Extensions",
                        "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
                }

            }

            string dotnetRoot = Path.GetDirectoryName(DotNetHostPath);
            if (Environment.Is64BitProcess)
            {
                command.Environment.Add("DOTNET_ROOT", dotnetRoot);
            }
            else
            {
                command.Environment.Add("DOTNET_ROOT(x86)", dotnetRoot);
            }

            DirectoryInfo latestSdk = GetLatestSdk(dotnetRoot);
            command.Environment["NETCoreSdkBundledVersionsProps"] = Path.Combine(latestSdk.FullName, "Microsoft.NETCoreSdk.BundledVersions.props");
        }

        private static DirectoryInfo GetLatestSdk(string dotnetRoot)
        {
            return new DirectoryInfo(Path.Combine(dotnetRoot, "sdk"))
                .EnumerateDirectories()
                .Where(d => NuGetVersion.TryParse(d.Name, out _))
                .OrderByDescending(d => NuGetVersion.Parse(d.Name))
                .First();
        }

        public SdkCommandSpec CreateCommandForTarget(string target, params string[] args)
        {

            var newArgs = args.ToList();
            newArgs.Insert(0, $"/t:{target}");

            return CreateCommand(newArgs.ToArray());
        }

        private SdkCommandSpec CreateCommand(params string[] args)
        {
            SdkCommandSpec ret = new SdkCommandSpec();

            //  Run tests on full framework MSBuild if environment variable is set pointing to it
            if (ShouldUseFullFrameworkMSBuild)
            {
                ret.FileName = FullFrameworkMSBuildPath;
                ret.Arguments = args.ToList();
            }
            else
            {
                var newArgs = args.ToList();
                newArgs.Insert(0, $"msbuild");

                ret.FileName = DotNetHostPath;
                ret.Arguments = newArgs;
            }

            TestContext.Current.AddTestEnvironmentVariables(ret);

            return ret;
        }
        public static ToolsetInfo Create(string repoRoot, string repoArtifactsDir, string configuration, TestCommandLine commandLine)
        {
            var ret = new ToolsetInfo();

            repoRoot = commandLine.SDKRepoPath ?? repoRoot;
            configuration = commandLine.SDKRepoConfiguration ?? configuration;

            string dotnetInstallDirFromEnvironment = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");

            if (!string.IsNullOrEmpty(commandLine.DotnetHostPath))
            {
                ret.DotNetHostPath = commandLine.DotnetHostPath;
            }
            else if (!string.IsNullOrEmpty(dotnetInstallDirFromEnvironment))
            {
                ret.DotNetHostPath = Path.Combine(dotnetInstallDirFromEnvironment, $"dotnet{Constants.ExeSuffix}");
            }
            else if (repoRoot != null)
            {
                ret.DotNetHostPath = Path.Combine(repoRoot, ".dotnet", $"dotnet{Constants.ExeSuffix}");
            }
            else
            {
                ret.DotNetHostPath = ResolveCommand("dotnet");
            }

            if (repoRoot != null)
            {
                ret.SdksPath = Path.Combine(repoArtifactsDir, "bin", configuration, "Sdks");
            }

            if (!string.IsNullOrEmpty(commandLine.FullFrameworkMSBuildPath))
            {
                ret.FullFrameworkMSBuildPath = commandLine.FullFrameworkMSBuildPath;
            }
            else if (commandLine.UseFullFrameworkMSBuild)
            {
                ret.FullFrameworkMSBuildPath = ResolveCommand("MSBuild");
            }

            return ret;
        }

        private static string ResolveCommand(string command)
        {
            char pathSplitChar;
            string[] extensions = new string[] { string.Empty };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                pathSplitChar = ';';
                extensions = extensions
                    .Concat(Environment.GetEnvironmentVariable("PATHEXT").Split(pathSplitChar))
                    .ToArray();
            }
            else
            {
                pathSplitChar = ':';                
            }

            var paths = Environment.GetEnvironmentVariable("PATH").Split(pathSplitChar);
            string result = extensions.SelectMany(ext => paths.Select(p => Path.Combine(p, command + ext)))
                .FirstOrDefault(File.Exists);

            if (result == null)
            {
                throw new InvalidOperationException("Could not resolve path to " + command);
            }

            return result;
        }

        private static string FindFileInTree(string relativePath, string startPath, bool throwIfNotFound = true)
        {
            string currentPath = startPath;
            while (true)
            {
                string path = Path.Combine(currentPath, relativePath);
                if (File.Exists(path))
                {
                    return path;
                }
                var parent = Directory.GetParent(currentPath);
                if (parent == null)
                {
                    if (throwIfNotFound)
                    {
                        throw new FileNotFoundException($"Could not find file '{relativePath}' in '{startPath}' or any of its ancestors");
                    }
                    else
                    {
                        return null;
                    }
                }
                currentPath = parent.FullName;
            }
        }

        private static bool UsingFullMSBuildWithoutExtensionsTargets()
        {
            string fullMSBuildPath = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILD_PATH");
            if (string.IsNullOrEmpty(fullMSBuildPath))
            {
                return false;
            }
            string fullMSBuildDirectory = Path.GetDirectoryName(fullMSBuildPath);
            string extensionsImportAfterPath = Path.Combine(fullMSBuildDirectory, "..", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
            return !File.Exists(extensionsImportAfterPath);
        }

    }
}
