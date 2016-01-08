// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class EndToEndTest : TestBase
    {
        private static readonly string s_expectedOutput = "Hello World!" + Environment.NewLine;
        private static readonly string s_testdirName = "e2etestroot";
        private static readonly string s_outputdirName = "testbin";
        
        private string Rid { get; set; }
        private string TestDirectory { get; set; }
        private string TestProject { get; set; }
        private string OutputDirectory { get; set; }

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }
       
        public EndToEndTest()
        {
            TestSetup();

            Rid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();
        }

        [Fact]
        public void TestDotnetBuild()
        {
            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory);

            buildCommand.Execute().Should().Pass();

            TestOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName());
        }

        [Fact]
        [ActiveIssue(712, PlatformID.Windows | PlatformID.OSX | PlatformID.Linux)]
        public void TestDotnetBuildNativeRyuJit()
        {
            if(IsCentOS())
            {
                Console.WriteLine("Skipping native compilation tests on CentOS - https://github.com/dotnet/cli/issues/453");
                return;
            }

            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, native: true);

            buildCommand.Execute().Should().Pass();

            var nativeOut = Path.Combine(OutputDirectory, "native");
            TestOutputExecutable(nativeOut, buildCommand.GetOutputExecutableName());
        }

        [Fact]
        public void TestDotnetBuildNativeCpp()
        {
            if(IsCentOS())
            {
                Console.WriteLine("Skipping native compilation tests on CentOS - https://github.com/dotnet/cli/issues/453");
                return;
            }

            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, native: true, nativeCppMode: true);

            buildCommand.Execute().Should().Pass();

            var nativeOut = Path.Combine(OutputDirectory, "native");
            TestOutputExecutable(nativeOut, buildCommand.GetOutputExecutableName());
        }

        [Fact]
        public void TestDotnetRun()
        {
            var runCommand = new RunCommand(TestProject);

            runCommand.Execute()
                .Should()
                .Pass();
        }
        
        [Fact]
        public void TestDotnetPack()
        {
            var packCommand = new PackCommand(TestDirectory, output: OutputDirectory);

            packCommand.Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void TestDotnetPublish()
        {
            var publishCommand = new PublishCommand(TestProject, output: OutputDirectory);
            publishCommand.Execute().Should().Pass();

            TestOutputExecutable(OutputDirectory, publishCommand.GetOutputExecutable());    
        }

        private void TestSetup()
        {
            var root = Temp.CreateDirectory();

            TestDirectory = root.CreateDirectory(s_testdirName).Path;
            TestProject = Path.Combine(TestDirectory, "project.json");
            OutputDirectory = Path.Combine(TestDirectory, s_outputdirName);

            InitializeTestDirectory();   
        }

        private void InitializeTestDirectory()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(TestDirectory);

            new NewCommand().Execute().Should().Pass();
            new RestoreCommand().Execute("--quiet").Should().Pass();

            Directory.SetCurrentDirectory(currentDirectory);
        }

        private void TestOutputExecutable(string outputDir, string executableName)
        {
            var executablePath = Path.Combine(outputDir, executableName);

            var executableCommand = new TestCommand(executablePath);

            var result = executableCommand.ExecuteWithCapturedOutput("");

            result.Should().HaveStdOut(s_expectedOutput);
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }

        private bool IsCentOS()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                const string OSIDFILE = "/etc/os-release";

                if(File.Exists(OSIDFILE))
                {
                    return File.ReadAllText(OSIDFILE).ToLower().Contains("centos");
                }
            }

            return false;
        }
    }
}