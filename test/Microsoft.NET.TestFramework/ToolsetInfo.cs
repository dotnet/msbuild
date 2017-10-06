using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.NET.TestFramework
{
    public class ToolsetInfo
    {
        public string CliVersionForBundledVersions { get; set; }

        public string DotNetHostPath { get; set; }

        public string SdksPath { get; set; }

        public string BuildExtensionsSdkPath
        {
            get
            {
                if (string.IsNullOrEmpty(SdksPath))
                {
                    return null;
                }
                return Path.Combine(SdksPath, "Microsoft.NET.Build.Extensions");
            }
        }

        public string BuildExtensionsMSBuildPath
        {
            get
            {
                if (string.IsNullOrEmpty(BuildExtensionsSdkPath))
                {
                    return null;
                }
                return Path.Combine(BuildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions");
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
            }

            if (!string.IsNullOrEmpty(CliVersionForBundledVersions))
            {
                string dotnetRoot = Path.GetDirectoryName(DotNetHostPath);
                string stage0SdkPath = Path.Combine(dotnetRoot, "sdk", CliVersionForBundledVersions); ;
                command.Environment["NETCoreSdkBundledVersionsProps"] = Path.Combine(stage0SdkPath, "Microsoft.NETCoreSdk.BundledVersions.props");
            }
            command.Environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(BuildExtensionsMSBuildPath, "Microsoft.NET.Build.Extensions.targets");

            if (UsingFullMSBuildWithoutExtensionsTargets())
            {
                command.Environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(BuildExtensionsSdkPath,
                    "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
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

        public static ToolsetInfo Create(string repoRoot, string configuration, TestCommandLine commandLine)
        {
            var ret = new ToolsetInfo();

            repoRoot = commandLine.SDKRepoPath ?? repoRoot;
            configuration = commandLine.SDKRepoConfiguration ?? configuration;

            if (repoRoot != null)
            {
                ret.CliVersionForBundledVersions = File.ReadAllText(Path.Combine(repoRoot, "DotnetCLIVersion.txt")).Trim();
                ret.DotNetHostPath = Path.Combine(repoRoot, ".dotnet_cli", $"dotnet{Constants.ExeSuffix}");
                ret.SdksPath = Path.Combine(repoRoot, "bin", configuration, "Sdks");
            }
            else
            {
                ret.DotNetHostPath = ResolveCommand("dotnet");
                if (ret.DotNetHostPath == null)
                {
                    throw new InvalidOperationException("Could not resolve path to dotnet");
                }
            }

            ret.FullFrameworkMSBuildPath = commandLine.FullFrameworkMSBuildPath;

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
