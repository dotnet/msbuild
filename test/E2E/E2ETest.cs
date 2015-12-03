// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace ConsoleApplication
{
    public class E2ETest
    {
        private static readonly string EXPECTED_OUTPUT = "Hello World!" + Environment.NewLine;
        private static readonly string TESTDIR_NAME = "hellotest";
        private static readonly string OUTPUTDIR_NAME = "testbin";

        private static string RootPath { get; set; }
        private string TestDirectory { get; set; }
        private string OutputDirectory { get; set; }
        private string Rid { get; set; }

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }
       
        public E2ETest()
        {
            if (RootPath == null)
            {
                RootPath = Directory.GetCurrentDirectory();
            }

            TestDirectory = Path.Combine(RootPath, TESTDIR_NAME);
            OutputDirectory = Path.Combine(RootPath, OUTPUTDIR_NAME);

            Rid = RuntimeIdentifier.Current;
        }

        [Fact]
        public void TestDotnetCompile()
        {
            TestSetup();

            TestRunCommand("dotnet", $"compile -o {OutputDirectory}");
            TestOutputExecutable(OutputDirectory);
        }

        [Fact]
        public void TestDotnetCompileNativeRyuJit()
        {
            // Skip this test on mac
            if(SkipForOS(OSPlatform.OSX, "https://github.com/dotnet/cli/issues/246"))
            {
                return;
            }

            TestSetup();

            TestRunCommand("dotnet", $"compile --native -o {OutputDirectory}");

            var nativeOut = Path.Combine(OutputDirectory, "native");
            TestOutputExecutable(nativeOut);
        }

        [Fact]
        public void TestDotnetCompileNativeCpp()
        {
            // Skip this test on windows
            if(SkipForOS(OSPlatform.Windows, "https://github.com/dotnet/cli/issues/335"))
            {
                return;
            }

            TestSetup();

            TestRunCommand("dotnet", $"compile --native --cpp -o {OutputDirectory}");
            
            var nativeOut = Path.Combine(OutputDirectory, "native");
            TestOutputExecutable(nativeOut);
        }

        [Fact]
        public void TestDotnetRun()
        {
            TestSetup();

            TestRunCommand("dotnet", $"run");
        }

        [Fact(Skip="https://github.com/dotnet/cli/issues/333")]
        public void TestDotnetPack()
        {
            TestSetup();

            TestRunCommand("dotnet", $"pack");
        }

        [Fact]
        public void TestDotnetPublish()
        {
            TestSetup();

            TestRunCommand("dotnet", $"publish --framework dnxcore50 --runtime {Rid} -o {OutputDirectory}");
            TestOutputExecutable(OutputDirectory);    
        }

        private void TestSetup()
        {
            Directory.SetCurrentDirectory(RootPath);

            CleanOrCreateDirectory(TestDirectory);
            CleanOrCreateDirectory(OutputDirectory);

            Directory.SetCurrentDirectory(TestDirectory);

            TestRunCommand("dotnet", "init");
            TestRunCommand("dotnet", "restore --quiet");
        }

        private bool SkipForOS(OSPlatform os, string reason)
        {
            if (RuntimeInformation.IsOSPlatform(os))
            {
                Console.WriteLine("Skipping Test for reason: " + reason);
                return true;
            }
            return false;
        }

        private void TestRunCommand(string command, string args)
        {
            var result = Command.Create(command, args)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            Assert.Equal(0, result.ExitCode);
        }

        private void TestOutputExecutable(string outputDir)
        {
            var executableName = TESTDIR_NAME + Constants.ExeSuffix;

            var executablePath = Path.Combine(outputDir, executableName);

            var result = Command.Create(executablePath, "")
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            var outText = result.StdOut;
            Assert.Equal(EXPECTED_OUTPUT, outText);
        }

        private void CleanOrCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }
    }
}
