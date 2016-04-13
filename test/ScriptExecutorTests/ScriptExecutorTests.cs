using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Runtime.InteropServices;
using Xunit;
using FluentAssertions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils.ScriptExecutorTests
{
    public class ScriptExecutorTests : TestBase
    {
        private static readonly string s_testProjectRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets/TestProjects");

        private TempDirectory _root;
        private string binTestProjectPath;
        private Project project;

        public ScriptExecutorTests()
        {
            _root = Temp.CreateDirectory();

            var sourceTestProjectPath = Path.Combine(s_testProjectRoot, "TestApp");
            binTestProjectPath = _root.CopyDirectory(sourceTestProjectPath).Path;
            project = ProjectContext.Create(binTestProjectPath, NuGetFramework.Parse("netcoreapp1.0")).ProjectFile;
        }

        [Fact]
        public void Test_Project_Local_Script_is_Resolved()
        {
            CreateTestFile("some.script", binTestProjectPath);
            var scriptCommandLine = "some.script";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());

            command.Should().NotBeNull();
            command.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);
        }
        
        [Fact]
        public void Test_Nonexistent_Project_Local_Script_throws_CommandUnknownException()
        {
            var scriptCommandLine = "nonexistent.script";

            Action action = () => ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());
            action.ShouldThrow<CommandUnknownException>();
        }
        
        [Fact]
        public void Test_Extension_sh_is_Inferred_over_cmd_in_Project_Local_Scripts_on_Unix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var extensionList = new string[] { ".cmd", ".sh" };

            var expectedExtension = ".sh";

            foreach (var extension in extensionList)
            {
                CreateTestFile("uniquescriptname" + extension, binTestProjectPath);
            }

            // Don't include extension
            var scriptCommandLine = "uniquescriptname";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());

            command.Should().NotBeNull();
            command.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);
            command.CommandArgs.Should().Contain(scriptCommandLine + expectedExtension);
        }

        [Fact]
        public void Test_Extension_cmd_is_Inferred_over_sh_in_Project_Local_Scripts_on_Windows()
        {
            if (! RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var extensionList = new string[] { ".cmd", ".sh" };

            var expectedExtension = ".cmd";

            foreach (var extension in extensionList)
            {
                CreateTestFile("uniquescriptname" + extension, binTestProjectPath);
            }

            // Don't include extension
            var scriptCommandLine = "uniquescriptname";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());

            command.Should().NotBeNull();
            command.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);
            command.CommandArgs.Should().Contain(scriptCommandLine + expectedExtension);
        }

        [Fact]
        public void Test_Script_Exe_Files_Dont_Use_Cmd_or_Sh()
        {
            CreateTestFile("some.exe", binTestProjectPath);
            var scriptCommandLine = "some.exe";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());

            command.Should().NotBeNull();
            command.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);

            Path.GetFileName(command.CommandName).Should().NotBe("cmd.exe");
            Path.GetFileName(command.CommandName).Should().NotBe("sh");
        }

        [Fact]
        public void Test_Script_Cmd_Files_Use_CmdExe()
        {
            if (! RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            CreateTestFile("some.cmd", binTestProjectPath);
            var scriptCommandLine = "some.cmd";

            var command = ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());

            command.Should().NotBeNull();
            command.ResolutionStrategy.Should().Be(CommandResolutionStrategy.ProjectLocal);

            Path.GetFileName(command.CommandName).Should().Be("cmd.exe");
        }
        
        [Fact]
        public void Test_Script_Builtins_throws_CommandUnknownException()
        {
            var scriptCommandLine = "echo";

            Action action = () => ScriptExecutor.CreateCommandForScript(project, scriptCommandLine, new Dictionary<string, string>());
            action.ShouldThrow<CommandUnknownException>();
        }

        private void CreateTestFile(string filename, string directory)
        {
            string path = Path.Combine(directory, filename);
            File.WriteAllText(path, "echo hello");
        }
    }
}
