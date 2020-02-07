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
        public string DotNetRoot { get; }
        public string DotNetHostPath { get; }

        private string _sdkVersion;
        public string SdkVersion
        {
            get
            {
                if (_sdkVersion == null)
                {
                    //  Initialize SdkVersion lazily, as we call `dotnet --version` to get it, so we need to wait
                    //  for the TestContext to finish being initialize
                    InitSdkVersion();
                }
                return _sdkVersion;
            }
        }

        Lazy<string> _sdkFolderUnderTest;

        public string SdkFolderUnderTest => _sdkFolderUnderTest.Value;

        Lazy<string> _sdksPath;
        public string SdksPath => _sdksPath.Value;

        public string CliHomePath { get; set; }

        public string MicrosoftNETBuildExtensionsPathOverride { get; set; }

        public bool ShouldUseFullFrameworkMSBuild => !string.IsNullOrEmpty(FullFrameworkMSBuildPath);

        public string FullFrameworkMSBuildPath { get; set; }

        public ToolsetInfo(string dotNetRoot)
        {
            DotNetRoot = dotNetRoot;

            DotNetHostPath = Path.Combine(dotNetRoot, $"dotnet{Constants.ExeSuffix}");

            _sdkFolderUnderTest = new Lazy<string>(() => Path.Combine(DotNetRoot, "sdk", SdkVersion));
            _sdksPath = new Lazy<string>(() => Path.Combine(SdkFolderUnderTest, "Sdks"));
        }

        private void InitSdkVersion()
        {
            //  If using full framework MSBuild, then running a command tries to get the SdkVersion in order to set the
            //  DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR environment variable.  So turn that off when getting the SDK version
            //  in order to avoid stack overflow
            string oldFullFrameworkMSBuildPath = FullFrameworkMSBuildPath;
            try
            {
                FullFrameworkMSBuildPath = null;
                var logger = new StringTestLogger();
                var command = new DotnetCommand(logger, "--version");

                command.WorkingDirectory = TestContext.Current.TestExecutionDirectory;

                var result = command.Execute();

                if (result.ExitCode != 0)
                {
                    throw new Exception("Failed to get dotnet version" + Environment.NewLine + logger.ToString());
                }

                _sdkVersion = result.StdOut.Trim();
            }
            finally
            {
                FullFrameworkMSBuildPath = oldFullFrameworkMSBuildPath;
            }
        }

        public string GetMicrosoftNETBuildExtensionsPath()
        {
            if (!string.IsNullOrEmpty(MicrosoftNETBuildExtensionsPathOverride))
            {
                return MicrosoftNETBuildExtensionsPathOverride;
            }
            else
            {                
                if (ShouldUseFullFrameworkMSBuild)
                {
                    var msbuildBinPath = Path.GetDirectoryName(FullFrameworkMSBuildPath);
                    var msbuildRoot = Directory.GetParent(msbuildBinPath).Parent.FullName;
                    return Path.Combine(msbuildRoot, @"Microsoft\Microsoft.NET.Build.Extensions");
                }
                else
                {
                    return Path.Combine(DotNetRoot, "sdk", SdkVersion, @"Microsoft\Microsoft.NET.Build.Extensions");
                }
            }
        }

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            if (ShouldUseFullFrameworkMSBuild)
            {
                string sdksPath = Path.Combine(DotNetRoot, "sdk", SdkVersion, "Sdks");
                command.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = sdksPath;

                if (!string.IsNullOrEmpty(MicrosoftNETBuildExtensionsPathOverride))
                {
                    var microsoftNETBuildExtensionsPath = GetMicrosoftNETBuildExtensionsPath();
                    command.Environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(microsoftNETBuildExtensionsPath, "Microsoft.NET.Build.Extensions.targets");

                    if (UsingFullMSBuildWithoutExtensionsTargets())
                    {
                        command.Environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(sdksPath, "Microsoft.NET.Build.Extensions",
                            "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
                    }
                }

            }

            if (Environment.Is64BitProcess)
            {
                command.Environment.Add("DOTNET_ROOT", DotNetRoot);
            }
            else
            {
                command.Environment.Add("DOTNET_ROOT(x86)", DotNetRoot);
            }

            command.Environment.Add("DOTNET_CLI_HOME", CliHomePath);

            //  We set this environment variable for in-process tests, but we don't want it to flow to out of process tests
            //  (especially if we're trying to run on full Framework MSBuild)
            command.Environment[DotNet.Cli.Utils.Constants.MSBUILD_EXE_PATH] = "";

        }

        public SdkCommandSpec CreateCommandForTarget(string target, IEnumerable<string> args)
        {
            var newArgs = args.ToList();
            if (!string.IsNullOrEmpty(target))
            {
                newArgs.Insert(0, $"/t:{target}");
            }

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
                // Don't propagate DOTNET_HOST_PATH to the msbuild process, to match behavior
                // when running desktop msbuild outside of the test harness.
                ret.Environment["DOTNET_HOST_PATH"] = null;
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
            repoRoot = commandLine.SDKRepoPath ?? repoRoot;
            configuration = commandLine.SDKRepoConfiguration ?? configuration;

            string dotnetInstallDirFromEnvironment = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");

            string dotnetRoot;

            if (!string.IsNullOrEmpty(commandLine.DotnetHostPath))
            {
                dotnetRoot = Path.GetDirectoryName(commandLine.DotnetHostPath);
            }
            else if (repoRoot != null)
            {
                dotnetRoot = Path.Combine(repoArtifactsDir, "bin", "redist", configuration, "dotnet");
            }
            else if (!string.IsNullOrEmpty(dotnetInstallDirFromEnvironment))
            {
                dotnetRoot = dotnetInstallDirFromEnvironment;
            }
            else
            {
                dotnetRoot = Path.GetDirectoryName(ResolveCommand("dotnet"));
            }

            var ret = new ToolsetInfo(dotnetRoot);
            
            // if (!string.IsNullOrWhiteSpace(commandLine.MSBuildSDKsPath))
            // {
            //     ret.SdksPath = commandLine.MSBuildSDKsPath;
            // }
            // else if (repoRoot != null)
            // {
            //     ret.SdksPath = Path.Combine(repoArtifactsDir, "bin", configuration, "Sdks");
            // }

            if (!string.IsNullOrEmpty(commandLine.FullFrameworkMSBuildPath))
            {
                ret.FullFrameworkMSBuildPath = commandLine.FullFrameworkMSBuildPath;
            }
            else if (commandLine.UseFullFrameworkMSBuild)
            {
                ret.FullFrameworkMSBuildPath = ResolveCommand("MSBuild");
            }

            if (repoRoot != null && ret.ShouldUseFullFrameworkMSBuild)
            {
                //  Find path to Microsoft.NET.Build.Extensions for full framework
                string sdksPath = Path.Combine(repoArtifactsDir, "bin", configuration, "Sdks");
                var buildExtensionsSdkPath = Path.Combine(sdksPath, "Microsoft.NET.Build.Extensions");
                ret.MicrosoftNETBuildExtensionsPathOverride = Path.Combine(buildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions");
            }

            if (repoRoot != null)
            {
                ret.CliHomePath = Path.Combine(repoArtifactsDir, "tmp", configuration);
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

        private bool UsingFullMSBuildWithoutExtensionsTargets()
        {
            if (!ShouldUseFullFrameworkMSBuild)
            {
                return false;
            }
            string fullMSBuildDirectory = Path.GetDirectoryName(FullFrameworkMSBuildPath);
            string extensionsImportAfterPath = Path.Combine(fullMSBuildDirectory, "..", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
            return !File.Exists(extensionsImportAfterPath);
        }

    }
}
