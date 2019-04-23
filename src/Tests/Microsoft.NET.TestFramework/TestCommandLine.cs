using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class TestCommandLine
    {
        public List<string> RemainingArgs { get; private set; }

        public bool UseFullFrameworkMSBuild { get; private set; }

        public string FullFrameworkMSBuildPath { get; private set; }

        public string DotnetHostPath { get; private set; }

        public string SDKRepoPath { get; private set; }

        public string SDKRepoConfiguration { get; private set; }

        public bool NoRepoInference { get; private set; }

        public bool ShouldShowHelp { get; private set; }

        public string SdkVersion { get; private set; }

        public string TestExecutionDirectory { get; set; }

        public bool ShowSdkInfo { get; private set; }

        public static TestCommandLine Parse(string[] args)
        {
            TestCommandLine ret = new TestCommandLine();
            ret.RemainingArgs = new List<string>();
            Stack<string> argStack = new Stack<string>(args.Reverse());

            while (argStack.Any())
            {
                string arg = argStack.Pop();
                if (arg.Equals("-useFullMSBuild", StringComparison.InvariantCultureIgnoreCase))
                {
                    ret.UseFullFrameworkMSBuild = true;
                }
                else if (arg.Equals("-fullMSBuildPath", StringComparison.InvariantCultureIgnoreCase) && argStack.Any())
                {
                    ret.FullFrameworkMSBuildPath = argStack.Pop();
                }
                else if (arg.Equals("-dotnetPath", StringComparison.InvariantCultureIgnoreCase) && argStack.Any())
                {
                    ret.DotnetHostPath = argStack.Pop();
                }
                else if (arg.Equals("-sdkRepo", StringComparison.InvariantCultureIgnoreCase) && argStack.Any())
                {
                    ret.SDKRepoPath = argStack.Pop();
                }
                else if (arg.Equals("-sdkConfig", StringComparison.InvariantCultureIgnoreCase) && argStack.Any())
                {
                    ret.SDKRepoConfiguration = argStack.Pop();
                }
                else if (arg.Equals("-noRepoInference", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.NoRepoInference = true;
                }
                else if (arg.Equals("-sdkVersion", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.SdkVersion = argStack.Pop();
                }
                else if (arg.Equals("-testExecutionDirectory", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.TestExecutionDirectory = argStack.Pop();
                }
                else if (arg.Equals("-showSdkInfo", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.ShowSdkInfo = true;
                }
                else if (arg.Equals("-help", StringComparison.CurrentCultureIgnoreCase) || arg.Equals("/?"))
                {
                    ret.ShouldShowHelp = true;
                }
                else
                {
                    ret.RemainingArgs.Add(arg);
                }
            }

            if (!string.IsNullOrEmpty(ret.SDKRepoPath) && string.IsNullOrEmpty(ret.SDKRepoConfiguration))
            {
                ret.SDKRepoConfiguration = "Release";
            }

            if (string.IsNullOrEmpty(ret.FullFrameworkMSBuildPath))
            {
                //  Run tests on full framework MSBuild if environment variable is set pointing to it
                string msbuildPath = Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_MSBUILD_PATH");
                if (!string.IsNullOrEmpty(msbuildPath))
                {
                    ret.FullFrameworkMSBuildPath = msbuildPath;
                }
            }

            return ret;
        }

        public static TestCommandLine HandleCommandLine(string [] args)
        {
            TestCommandLine commandLine = Parse(args);

            if (!commandLine.ShouldShowHelp)
            {
                TestContext.Initialize(commandLine);
            }

            return commandLine;
        }

        public static void ShowHelp()
        {
            Console.WriteLine(
@"

.NET Core SDK test runner

Options to control toolset to test:
  -useFullMSBuild         : Use full framework (instead of .NET Core) version of MSBuild found in PATH
  -fullMSBuildPath <path> : Use full framework version of MSBuild in specified path
  -dotnetPath <path>      : Use specified path for dotnet host
  -sdkRepo <path>         : Use specified SDK repo for Microsoft.NET.SDK tasks / targets
  -sdkConfig <config>     : Use specified configuration for SDK repo
  -noRepoInference        : Don't automatically find SDK repo to use based on path to test binaries
  -sdkVersion             : Use specified SDK version
  -testExecutionDirectory : Folder for tests to create and build projects
  -showSdkInfo            : Shows SDK info (dotnet --info) for SDK which will be used
  -help                   : Show help");
        }
    }
}
