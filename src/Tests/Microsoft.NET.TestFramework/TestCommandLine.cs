// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

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

        public string MsbuildAdditionalSdkResolverFolder { get; set; }

        public List<string> TestConfigFiles { get; private set; } = new List<string>();

        public HashSet<string> TestListsToRun { get; private set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                else if (arg.Equals("-msbuildAdditionalSdkResolverFolder", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.MsbuildAdditionalSdkResolverFolder = argStack.Pop();
                }
                else if (arg.Equals("-testConfigFile", StringComparison.CurrentCultureIgnoreCase) ||
                         arg.Equals("-testConfig", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.TestConfigFiles.Add(argStack.Pop());
                }
                else if (arg.Equals("-testList", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.TestListsToRun.Add(argStack.Pop());
                }
                else if (arg.Equals("-showSdkInfo", StringComparison.CurrentCultureIgnoreCase))
                {
                    ret.ShowSdkInfo = true;
                }
                else if (arg.Equals("-help", StringComparison.CurrentCultureIgnoreCase) ||
                         arg.Equals("--help", StringComparison.CurrentCultureIgnoreCase) ||
                         arg.Equals("/?") || arg.Equals("-?"))
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

        public List<string> GetXunitArgsFromTestConfig()
        {
            List<TestSpecifier> testsToSkip = new List<TestSpecifier>();
            List<TestList> testLists = new List<TestList>();

            List<string> ret = new List<string>();
            foreach (var testConfigFile in TestConfigFiles)
            {
                var testConfig = XDocument.Load(testConfigFile);
                foreach (var item in testConfig.Root.Elements())
                {
                    if (item.Name.LocalName.Equals("TestList", StringComparison.OrdinalIgnoreCase))
                    {
                        testLists.Add(TestList.Parse(item));
                    }
                    else if (item.Name.LocalName.Equals("SkippedTests", StringComparison.OrdinalIgnoreCase))
                    {
                        var skippedGroup = TestList.Parse(item);
                        testsToSkip.AddRange(skippedGroup.TestSpecifiers);
                    }
                    else
                    {
                        if (bool.TryParse(item.Attribute("Skip")?.Value ?? string.Empty, out bool shouldSkip) &&
                            shouldSkip)
                        {
                            testsToSkip.Add(TestSpecifier.Parse(item));
                        }
                    }
                }
            }

            foreach (var testList in testLists.Where(g => TestListsToRun.Contains(g.Name)))
            {
                foreach (var testSpec in testList.TestSpecifiers)
                {
                    if (testSpec.Type == TestSpecifier.TestSpecifierType.Method)
                    {
                        ret.Add("-method");
                    }
                    else if (testSpec.Type == TestSpecifier.TestSpecifierType.Class)
                    {
                        ret.Add("-class");
                    }
                    else if (testSpec.Type == TestSpecifier.TestSpecifierType.Namespace)
                    {
                        ret.Add("-namespace");
                    }
                    else
                    {
                        throw new ArgumentException("Unrecognized test specifier type: " + testSpec.Type);
                    }
                    ret.Add(testSpec.Specifier);
                }
            }

            foreach (var testSpec in testsToSkip)
            {
                if (testSpec.Type == TestSpecifier.TestSpecifierType.Method)
                {
                    ret.Add("-nomethod");
                }
                else if (testSpec.Type == TestSpecifier.TestSpecifierType.Class)
                {
                    ret.Add("-noclass");
                }
                else if (testSpec.Type == TestSpecifier.TestSpecifierType.Namespace)
                {
                    ret.Add("-nonamespace");
                }
                else
                {
                    throw new ArgumentException("Unrecognized test specifier type: " + testSpec.Type);
                }
                ret.Add(testSpec.Specifier);
            }

            return ret;
        }

        private class TestList
        {
            public string Name { get; set; }

            public List<TestSpecifier> TestSpecifiers { get; set; } = new List<TestSpecifier>();

            public static TestList Parse(XElement element)
            {
                TestList group = new TestList();

                group.Name = element.Attribute("Name")?.Value;

                foreach (var item in element.Elements())
                {
                    group.TestSpecifiers.Add(TestSpecifier.Parse(item));
                }

                return group;
            }
        }

        private class TestSpecifier
        {
            public enum TestSpecifierType
            {
                Method,
                Class,
                Namespace
            }

            public TestSpecifierType Type { get; set; }
            public string Specifier { get; set; }

            public static TestSpecifier Parse(XElement element)
            {
                TestSpecifier spec = new TestSpecifier();
                switch (element.Name.LocalName.ToLowerInvariant())
                {
                    case "method":
                        spec.Type = TestSpecifierType.Method;
                        break;
                    case "class":
                        spec.Type = TestSpecifierType.Class;
                        break;
                    case "namespace":
                        spec.Type = TestSpecifierType.Namespace;
                        break;
                    default:
                        throw new XmlException("Unrecognized node: " + element.Name);
                }

                spec.Specifier = element.Attribute("Name").Value;

                return spec;
            }
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

Other options:
  -testConfigFile         : XML file with tests to skip, or test lists that can be run (can specify multiple)
  -testList               : List of tests (from config file) which should be run (can specify multiple)
  -testExecutionDirectory : Folder for tests to create and build projects
  -msbuildAdditionalSdkResolverFolder
                          : Folder for tests to override 'MsbuildAdditionalSdkResolverFolder' environment variable in tests
                            in order to test build built sdk resolvers.
  -showSdkInfo            : Shows SDK info (dotnet --info) for SDK which will be used
  -help                   : Show help");
        }
    }
}
