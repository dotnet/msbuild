using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class TestCommandLine
    {
        public List<string> RemainingArgs { get; private set; }

        public string FullFrameworkMSBuildPath { get; private set; }

        public string DotnetHostPath { get; private set; }

        public string SDKRepoPath { get; private set; }

        public string SDKRepoConfiguration { get; private set; }

        public bool NoRepoInference { get; private set; }

        public static TestCommandLine Parse(string[] args)
        {
            TestCommandLine ret = new TestCommandLine();
            ret.RemainingArgs = new List<string>();
            Stack<string> argStack = new Stack<string>(args.Reverse());

            while (argStack.Any())
            {
                string arg = argStack.Pop();
                if (arg.Equals("-fullMSBuild", StringComparison.InvariantCultureIgnoreCase) && argStack.Any())
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

        public static List<string> HandleCommandLine(string [] args)
        {
            TestCommandLine commandLine = Parse(args);

            TestContext.Initialize(commandLine);

            return commandLine.RemainingArgs;
        }
    }
}
