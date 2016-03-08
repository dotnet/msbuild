// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tests.EndToEnd
{
    public class EndToEndTest : TestBase
    {
        private static readonly string s_expectedOutput = "Hello World!" + Environment.NewLine;
        private static readonly string s_testdirName = "e2etestroot";
        private static readonly string s_outputdirName = "test space/bin";

        private static string RestoredTestProjectDirectory { get; set; }

        private string Rid { get; set; }
        private string TestDirectory { get; set; }
        private string TestProject { get; set; }
        private string OutputDirectory { get; set; }

        static EndToEndTest()
        {
            EndToEndTest.SetupStaticTestProject();
        }

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }

        public EndToEndTest()
        {
            TestInstanceSetup();
        }

        [Fact]
        public void TestDotnetBuild()
        {
            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, framework: DefaultFramework);

            buildCommand.Execute().Should().Pass();

            TestOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);
        }

        [Fact]
        public void TestDotnetIncrementalBuild()
        {
            // first build
            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, framework: DefaultFramework);
            buildCommand.Execute().Should().Pass();
            TestOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);

            var binariesOutputDirectory = GetCompilationOutputPath(OutputDirectory, false);
            var latestWriteTimeFirstBuild = GetLastWriteTimeUtcOfDirectoryFiles(
                binariesOutputDirectory);

            // second build; should get skipped (incremental because no inputs changed)
            buildCommand.Execute().Should().Pass();
            TestOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);

            var latestWriteTimeUtcSecondBuild = GetLastWriteTimeUtcOfDirectoryFiles(
                binariesOutputDirectory);
            Assert.Equal(latestWriteTimeFirstBuild, latestWriteTimeUtcSecondBuild);

            TouchSourceFileInDirectory(TestDirectory);

            // third build; should get compiled because the source file got touched
            buildCommand.Execute().Should().Pass();
            TestOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);

            var latestWriteTimeUtcThirdBuild = GetLastWriteTimeUtcOfDirectoryFiles(
                binariesOutputDirectory);
            Assert.NotEqual(latestWriteTimeUtcSecondBuild, latestWriteTimeUtcThirdBuild);
        }

        [Fact]
        public void TestDotnetBuildNativeRyuJit()
        {
            if(IsCentOS())
            {
                Console.WriteLine("Skipping native compilation tests on CentOS - https://github.com/dotnet/cli/issues/453");
                return;
            }

            if (IsWinX86())
            {
                Console.WriteLine("Skipping native compilation tests on Windows x86 - https://github.com/dotnet/cli/issues/1550");
                return;
            }

            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, native: true, framework: DefaultFramework);

            buildCommand.Execute().Should().Pass();

            TestNativeOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);
        }

        [Fact]
        public void TestDotnetBuildNativeCpp()
        {
            if(IsCentOS())
            {
                Console.WriteLine("Skipping native compilation tests on CentOS - https://github.com/dotnet/cli/issues/453");
                return;
            }

            if (IsWinX86())
            {
                Console.WriteLine("Skipping native compilation tests on Windows x86 - https://github.com/dotnet/cli/issues/1550");
                return;
            }

            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, native: true, nativeCppMode: true, framework: DefaultFramework);

            buildCommand.Execute().Should().Pass();

            TestNativeOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);
        }

        [Fact]
        public void TestDotnetCompileNativeCppIncremental()
        {
            if (IsCentOS())
            {
                Console.WriteLine("Skipping native compilation tests on CentOS - https://github.com/dotnet/cli/issues/453");
                return;
            }

            if (IsWinX86())
            {
                Console.WriteLine("Skipping native compilation tests on Windows x86 - https://github.com/dotnet/cli/issues/1550");
                return;
            }

            // first build
            var buildCommand = new BuildCommand(TestProject, output: OutputDirectory, native: true, nativeCppMode: true, framework: DefaultFramework);
            var binariesOutputDirectory = GetCompilationOutputPath(OutputDirectory, false);

            buildCommand.Execute().Should().Pass();

            TestNativeOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);

            var latestWriteTimeUtcFirstBuild = GetLastWriteTimeUtcOfDirectoryFiles(binariesOutputDirectory);

            // second build; should be skipped because nothing changed
            buildCommand.Execute().Should().Pass();

            TestNativeOutputExecutable(OutputDirectory, buildCommand.GetOutputExecutableName(), s_expectedOutput);

            var latestWriteTimeUtcSecondBuild = GetLastWriteTimeUtcOfDirectoryFiles(binariesOutputDirectory);
            Assert.Equal(latestWriteTimeUtcFirstBuild, latestWriteTimeUtcSecondBuild);
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

            TestExecutable(OutputDirectory, publishCommand.GetOutputExecutable(), s_expectedOutput);    
        }

        [Fact]
        public void TestDotnetHelp()
        {
            var helpCommand = new HelpCommand();
            helpCommand.Execute().Should().Pass();
        }

        [Fact]
        public void TestDotnetHelpBuild()
        {
            var helpCommand = new HelpCommand();
            helpCommand.Execute("build").Should().Pass();
        }

        private void TestInstanceSetup()
        {
            var root = Temp.CreateDirectory();

            var testInstanceDir = root.CopyDirectory(RestoredTestProjectDirectory);

            TestDirectory = testInstanceDir.Path;
            TestProject = Path.Combine(TestDirectory, "project.json");
            OutputDirectory = Path.Combine(TestDirectory, s_outputdirName);

            Rid = PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier();
        }

        private static void SetupStaticTestProject()
        {
            RestoredTestProjectDirectory = Path.Combine(AppContext.BaseDirectory, "bin", s_testdirName);

            // Ignore Delete Failure
            try
            {
                Directory.Delete(RestoredTestProjectDirectory, true);
            }
            catch(Exception) {}

            Directory.CreateDirectory(RestoredTestProjectDirectory);

            var currentDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(RestoredTestProjectDirectory);

            new NewCommand().Execute().Should().Pass();
            new RestoreCommand().Execute("--quiet").Should().Pass();

            Directory.SetCurrentDirectory(currentDirectory);
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

        private bool IsWinX86()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                RuntimeInformation.ProcessArchitecture == Architecture.X86;
        }

        private static DateTime GetLastWriteTimeUtcOfDirectoryFiles(string outputDirectory)
        {
            return Directory.EnumerateFiles(outputDirectory).Max(f => File.GetLastWriteTimeUtc(f));
        }

        private static void TouchSourceFileInDirectory(string directory)
        {
            var csFile = Directory.EnumerateFiles(directory).First(f => Path.GetExtension(f).Equals(".cs"));
            File.SetLastWriteTimeUtc(csFile, DateTime.UtcNow);
        }
    }
}
