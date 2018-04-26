using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Commands;
using System.Xml.Linq;
using System.Reflection;

namespace Microsoft.NET.TestFramework
{
    public class ToolsetInfo
    {
        public string CliVersionForBundledVersions { get; set; }

        public string DotNetHostPath { get; set; }

        public string SdksPath { get; set; }

        public string BuildExtensionsMSBuildPath
        {
            get
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
                        return Path.Combine(msbuildBinPath, @"Microsoft\Microsoft.NET.Build.Extensions");
                    }
                }
            }
        }

        public bool ShouldUseFullFrameworkMSBuild => !string.IsNullOrEmpty(FullFrameworkMSBuildPath);

        public string FullFrameworkMSBuildPath { get; set; }

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            if (SdksPath != null)
            {
                command.Environment["MSBuildSDKsPath"] = SdksPath;
                command.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = SdksPath;

                command.Environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(BuildExtensionsMSBuildPath, "Microsoft.NET.Build.Extensions.targets");

                if (UsingFullMSBuildWithoutExtensionsTargets())
                {
                    command.Environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(SdksPath, "Microsoft.NET.Build.Extensions",
                        "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
                }
            }

            if (!string.IsNullOrEmpty(CliVersionForBundledVersions))
            {
                string dotnetRoot = Path.GetDirectoryName(DotNetHostPath);
                string stage0SdkPath = Path.Combine(dotnetRoot, "sdk", CliVersionForBundledVersions); ;
                command.Environment["NETCoreSdkBundledVersionsProps"] = Path.Combine(stage0SdkPath, "Microsoft.NETCoreSdk.BundledVersions.props");
            }
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
                ret.CliVersionForBundledVersions = GetDotNetCliVersion();
                ret.SdksPath = Path.Combine(repoArtifactsDir, configuration, "bin", "Sdks");
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

        private static string GetDotNetCliVersion()
            => typeof(ToolsetInfo).Assembly.GetCustomAttribute<DotNetSdkVersionAttribute>().Version;

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
