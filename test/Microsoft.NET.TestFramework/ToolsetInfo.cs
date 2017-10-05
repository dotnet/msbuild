using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.NET.TestFramework
{
    public class ToolsetInfo
    {
        public string CliVersion { get; set; }

        public string DotNetHostPath { get; set; }

        private string Stage0SdkPath
        {
            get
            {
                string dotnetRoot = Path.GetDirectoryName(DotNetHostPath);
                return Path.Combine(dotnetRoot, "sdk", CliVersion);
            }
        }

        public string SdksPath { get; set; }

        public string BuildExtensionsSdkPath { get; set; }

        public string BuildExtensionsMSBuildPath { get; set; }

        public bool ShouldUseFullFrameworkMSBuild => !string.IsNullOrEmpty(FullFrameworkMSBuildPath);

        public string FullFrameworkMSBuildPath { get; set; }

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            if (SdksPath != null)
            {
                command.Environment["MSBuildSDKsPath"] = SdksPath;
                command.Environment["DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR"] = SdksPath;
            }

            command.Environment["NETCoreSdkBundledVersionsProps"] = Path.Combine(Stage0SdkPath, "Microsoft.NETCoreSdk.BundledVersions.props");

            if (UsingFullMSBuildWithoutExtensionsTargets())
            {
                command.Environment["CustomAfterMicrosoftCommonTargets"] = Path.Combine(BuildExtensionsSdkPath,
                    "msbuildExtensions-ver", "Microsoft.Common.targets", "ImportAfter", "Microsoft.NET.Build.Extensions.targets");
            }
            command.Environment["MicrosoftNETBuildExtensionsTargets"] = Path.Combine(BuildExtensionsMSBuildPath, "Microsoft.NET.Build.Extensions.targets");
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
