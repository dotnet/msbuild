using System;
using System.IO;
using Xunit;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;

namespace ConsoleApplication
{
    public class E2ETest
    {
        private static readonly string EXPECTED_OUTPUT = "Hello World!";
        private static readonly string TEST_PROJECT_NAME = "hellotest";
        private static readonly string OUTPUT_FOLDER_NAME = "testbin";

        public static void Main()
        {
            Console.WriteLine("Dummy Entrypoint.");
        }

        [Fact]
        public static void TestE2E()
        {
            // Setup Paths
            var rootPath = GetRootPath();
            var testDir = Path.Combine(rootPath, TEST_PROJECT_NAME);
            var outputDir = Path.Combine(testDir, OUTPUT_FOLDER_NAME);

            // Setup RID
            var rid = RuntimeIdentifier.Current;

            // Create Test Directory and cd there
            CleanOrCreateDirectory(testDir);
            Directory.SetCurrentDirectory(TEST_PROJECT_NAME);

            // Base Set of Commands
            TestRunCommand("dotnet", "init");

            TestRunCommand("dotnet", "restore");
            TestRunCommand("dotnet", "run");

            // Compile
            TestRunCommand("dotnet", $"compile -o {outputDir}");
            TestOutputExecutable(outputDir);

            // Native Compilation
            CleanOrCreateDirectory(outputDir);
            TestRunCommand("dotnet", $"compile --native -o {outputDir}");
            TestOutputExecutable(outputDir);

            // Native Compilation w/ CPP backend
            CleanOrCreateDirectory(outputDir);
            TestRunCommand("dotnet", $"compile --native --cpp -o {outputDir}");
            TestOutputExecutable(outputDir);

            // Publish
            CleanOrCreateDirectory(outputDir);
            TestRunCommand("dotnet", $"publish --framework dnxcore50 --runtime {rid} -o {outputDir}");
            TestOutputExecutable(outputDir);

            TestRunCommand("dotnet", "pack");
        }

        public static void TestRunCommand(string command, string args)
        {
            var result = Command.Create(command, args)
                .ForwardStdErr()
                .ForwardStdOut()
                .Execute();

            Assert.Equal(0, result.ExitCode);
        }

        public static void TestOutputExecutable(string outputDir)
        {
            var executableName = TEST_PROJECT_NAME + Constants.ExeSuffix;

            var executablePath = Path.Combine(outputDir, executableName);

            var result = Command.Create(executablePath, "")
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            var outText = result.StdOut;

            var expectedText = EXPECTED_OUTPUT + Environment.NewLine;
            Assert.Equal(expectedText, outText);
        }

        public static string GetRootPath()
        {
            var cwd = Directory.GetCurrentDirectory();

            return cwd;
        }

        public static void CleanOrCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }
    }
}
