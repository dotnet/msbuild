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
using System.CommandLine;

namespace Microsoft.NET.TestFramework
{
    public class ToolsetInfo
    {
        public const string CurrentTargetFramework = "net7.0";
        public const string CurrentTargetFrameworkVersion = "7.0";
        public const string NextTargetFramework = "net8.0";
        public const string NextTargetFrameworkVersion = "8.0";

        public const string LatestWinRuntimeIdentifier = "win10";
        public const string LatestLinuxRuntimeIdentifier = "ubuntu.22.04";
        public const string LatestMacRuntimeIdentifier = "osx.13";
        public const string LatestRuntimeIdentifiers = $"{LatestWinRuntimeIdentifier}-x64;{LatestWinRuntimeIdentifier}-x86;osx.10.10-x64;osx.10.11-x64;osx.10.12-x64;osx.10.14-x64;{LatestMacRuntimeIdentifier}-x64;ubuntu.14.04-x64;ubuntu.16.04-x64;ubuntu.16.10-x64;ubuntu.18.04-x64;ubuntu.20.04-x64;{LatestLinuxRuntimeIdentifier}-x64;centos.9-x64;rhel.9-x64;debian.9-x64;fedora.37-x64;opensuse.42.3-x64;linux-musl-x64";

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

        private string _msbuildVersion;
        public string MSBuildVersion
        {
            get
            {
                if (_msbuildVersion == null)
                {
                    //  Initialize MSBuildVersion lazily, as we call `dotnet msbuild -version` to get it, so we need to wait
                    //  for the TestContext to finish being initialize
                    InitMSBuildVersion();
                }
                return _msbuildVersion;
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

        public string SdkResolverPath { get; set; }

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

        private void InitMSBuildVersion()
        {
            var logger = new StringTestLogger();
            var command = new MSBuildVersionCommand(logger);

            command.WorkingDirectory = TestContext.Current.TestExecutionDirectory;

            var result = command.Execute();

            if (result.ExitCode != 0)
            {
                throw new Exception("Failed to get msbuild version" + Environment.NewLine + logger.ToString());
            }

            _msbuildVersion = result.StdOut.Split().Last();
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

        public void AddTestEnvironmentVariables(IDictionary<string, string> environment)
        {
            if (ShouldUseFullFrameworkMSBuild)
            {
                string sdksPath = Path.Combine(DotNetRoot, "sdk", SdkVersion, "Sdks");

                //  Use stage 2 MSBuild SDK resolver
                environment["MSBUILDADDITIONALSDKRESOLVERSFOLDER"] = SdkResolverPath;

                //  Avoid using stage 0 dotnet install dir
                environment["DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR"] = "";

                //  Put stage 2 on the Path (this is how the MSBuild SDK resolver finds dotnet)
                environment["Path"] = DotNetRoot + ";" + Environment.GetEnvironmentVariable("Path");

                if (!string.IsNullOrEmpty(MicrosoftNETBuildExtensionsPathOverride))
                {
                    var microsoftNETBuildExtensionsPath = GetMicrosoftNETBuildExtensionsPath();
                    environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(microsoftNETBuildExtensionsPath, "Microsoft.NET.Build.Extensions.targets");

                    if (UsingFullMSBuildWithoutExtensionsTargets())
                    {
                        environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(sdksPath, "Microsoft.NET.Build.Extensions",
                            "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
                    }
                }

            }

            if (Environment.Is64BitProcess)
            {
                environment.Add("DOTNET_ROOT", DotNetRoot);
            }
            else
            {
                environment.Add("DOTNET_ROOT(x86)", DotNetRoot);
            }

            if (!string.IsNullOrEmpty(CliHomePath))
            {
                environment.Add("DOTNET_CLI_HOME", CliHomePath);
            }

            //  We set this environment variable for in-process tests, but we don't want it to flow to out of process tests
            //  (especially if we're trying to run on full Framework MSBuild)
            environment[DotNet.Cli.Utils.Constants.MSBUILD_EXE_PATH] = "";

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

            TestContext.Current.AddTestEnvironmentVariables(ret.Environment);

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
                if (TryResolveCommand("dotnet", out string pathToDotnet))
                {
                    dotnetRoot = Path.GetDirectoryName(pathToDotnet);
                }
                else
                {
                    throw new InvalidOperationException("Could not resolve path to dotnet");
                }
            }

            var ret = new ToolsetInfo(dotnetRoot);

            if (!string.IsNullOrEmpty(commandLine.FullFrameworkMSBuildPath))
            {
                ret.FullFrameworkMSBuildPath = commandLine.FullFrameworkMSBuildPath;
            }
            else if (commandLine.UseFullFrameworkMSBuild)
            {
                if (TryResolveCommand("MSBuild", out string pathToMSBuild))
                {
                    ret.FullFrameworkMSBuildPath = Path.GetDirectoryName(pathToMSBuild);
                }
                else
                {
                    throw new InvalidOperationException("Could not resolve path to MSBuild");
                }
            }

            var microsoftNETBuildExtensionsTargetsFromEnvironment = Environment.GetEnvironmentVariable("MicrosoftNETBuildExtensionsTargets");
            if (!string.IsNullOrWhiteSpace(microsoftNETBuildExtensionsTargetsFromEnvironment))
            {
                ret.MicrosoftNETBuildExtensionsPathOverride = Path.GetDirectoryName(microsoftNETBuildExtensionsTargetsFromEnvironment);
            }
            else if (repoRoot != null && ret.ShouldUseFullFrameworkMSBuild)
            {
                //  Find path to Microsoft.NET.Build.Extensions for full framework
                string sdksPath = Path.Combine(repoArtifactsDir, "bin", configuration, "Sdks");
                var buildExtensionsSdkPath = Path.Combine(sdksPath, "Microsoft.NET.Build.Extensions");
                ret.MicrosoftNETBuildExtensionsPathOverride = Path.Combine(buildExtensionsSdkPath, "msbuildExtensions", "Microsoft", "Microsoft.NET.Build.Extensions");
            }

            if (ret.ShouldUseFullFrameworkMSBuild)
            {
                if (repoRoot != null)
                {
                    // Find path to MSBuildSdkResolver for full framework
                    ret.SdkResolverPath = Path.Combine(repoArtifactsDir, "bin", "Microsoft.DotNet.MSBuildSdkResolver", configuration, "net472", "SdkResolvers");
                }
                else if (!string.IsNullOrWhiteSpace(commandLine.MsbuildAdditionalSdkResolverFolder))
                {
                    ret.SdkResolverPath = Path.Combine(commandLine.MsbuildAdditionalSdkResolverFolder, configuration, "net472", "SdkResolvers");
                }
                else if (Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER") != null)
                {
                    ret.SdkResolverPath = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER"), configuration, "net472", "SdkResolvers");
                }
                else
                {
                    throw new InvalidOperationException("Microsoft.DotNet.MSBuildSdkResolver path is not provided, set msbuildAdditionalSdkResolverFolder on test commandline or set repoRoot");
                }
            }

            if (repoRoot != null)
            {
                ret.CliHomePath = Path.Combine(repoArtifactsDir, "tmp", configuration);
            }            

            return ret;
        }

        /// <summary>
        /// Attempts to resolve full path to command from PATH/PATHEXT environment variable.
        /// </summary>
        /// <param name="command">The command to resolve.</param>
        /// <param name="fullExePath">The full path to the command</param>
        /// <returns><see langword="true"/> when command can be resolved, <see langword="false"/> otherwise.</returns>
        public static bool TryResolveCommand(string command, out string fullExePath)
        {
            fullExePath = null;
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
                return false;
            }

            fullExePath = result;
            return true;
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

        private static readonly Lazy<string> _NewtonsoftJsonPackageVersion = new Lazy<string>(() =>
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            return assembly.GetCustomAttributes(true).OfType<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "NewtonsoftJsonPackageVersion").Value;
        });

        public static string GetNewtonsoftJsonPackageVersion()
        {
            return _NewtonsoftJsonPackageVersion.Value;
        }

    }
}
