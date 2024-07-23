﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class ToolTask_Tests
    {
        private ITestOutputHelper _output;

        public ToolTask_Tests(ITestOutputHelper testOutput)
        {
            _output = testOutput;
        }

        private class MyTool : ToolTask, IDisposable
        {
            private string _fullToolName;
            private string _responseFileCommands = string.Empty;
            private string _commandLineCommands = string.Empty;
            private string _pathToToolUsed;

            public MyTool()
                : base()
            {
                _fullToolName = Path.Combine(
                    NativeMethodsShared.IsUnixLike ? "/bin" : Environment.GetFolderPath(Environment.SpecialFolder.System),
                    NativeMethodsShared.IsUnixLike ? "sh" : "cmd.exe");
            }

            public void Dispose()
            {
            }

            public string PathToToolUsed => _pathToToolUsed;

            public string MockResponseFileCommands
            {
                set => _responseFileCommands = value;
            }

            public string MockCommandLineCommands
            {
                set => _commandLineCommands = value;
            }

            public string FullToolName
            {
                set => _fullToolName = value;
            }

            /// <summary>
            /// Intercepted start info
            /// </summary>
            internal ProcessStartInfo StartInfo { get; private set; }

            /// <summary>
            /// Whether execute was called
            /// </summary>
            internal bool ExecuteCalled { get; private set; }

            internal Action<ProcessStartInfo> DoProcessStartInfoMutation { get; set; }

            protected override string ToolName => Path.GetFileName(_fullToolName);

            protected override string GenerateFullPathToTool() => _fullToolName;

            protected override string GenerateResponseFileCommands() => _responseFileCommands;

            protected override string GenerateCommandLineCommands() => _commandLineCommands;

            protected override ProcessStartInfo GetProcessStartInfo(string pathToTool, string commandLineCommands, string responseFileSwitch)
            {
                var basePSI = base.GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);
                DoProcessStartInfoMutation?.Invoke(basePSI);
                return basePSI;
            }

            protected override void LogEventsFromTextOutput(string singleLine, MessageImportance messageImportance)
            {
                if (singleLine.Contains("BADTHINGHAPPENED"))
                {
                    // This is where a customer's tool task implementation could do its own
                    // parsing of the errors in the stdout/stderr output of the tool being wrapped.
                    Log.LogError(singleLine);
                }
                else
                {
                    base.LogEventsFromTextOutput(singleLine, messageImportance);
                }
            }

            protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
            {
                Console.WriteLine("executetool");
                _pathToToolUsed = pathToTool;
                ExecuteCalled = true;
                if (!NativeMethodsShared.IsWindows && string.IsNullOrEmpty(responseFileCommands) && string.IsNullOrEmpty(commandLineCommands))
                {
                    // Unix makes sh interactive and it won't exit if there is nothing on the command line
                    commandLineCommands = " -c \"echo\"";
                }

                int result = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
                StartInfo = GetProcessStartInfo(GenerateFullPathToTool(), NativeMethodsShared.IsWindows ? "/x" : string.Empty, null);
                return result;
            }
        }

        [Fact]
        public void Regress_Mutation_UserSuppliedToolPathIsLogged()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.ToolPath = NativeMethodsShared.IsWindows ? @"C:\MyAlternatePath" : "/MyAlternatePath";

                t.Execute();

                // The alternate path should be mentioned in the log.
                engine.AssertLogContains("MyAlternatePath");
            }
        }

        [Fact]
        public void Regress_Mutation_MissingExecutableIsLogged()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.ToolPath = NativeMethodsShared.IsWindows ? @"C:\MyAlternatePath" : "/MyAlternatePath";

                t.Execute().ShouldBeFalse();

                // There should be an error about invalid task location.
                engine.AssertLogContains("MSB6004");
            }
        }

        [Fact]
        public void Regress_Mutation_WarnIfCommandLineTooLong()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;

                // "cmd.exe" croaks big-time when given a very long command-line.  It pops up a message box on
                // Windows XP.  We can't have that!  So use "attrib.exe" for this exercise instead.
                string systemPath = NativeMethodsShared.IsUnixLike ? "/bin" : Environment.GetFolderPath(Environment.SpecialFolder.System);
                t.FullToolName = Path.Combine(systemPath, NativeMethodsShared.IsWindows ? "attrib.exe" : "ps");

                t.MockCommandLineCommands = new string('x', 32001);

                // It's only a warning, we still succeed
                t.Execute().ShouldBeTrue();
                t.ExitCode.ShouldBe(0);
                // There should be a warning about the command-line being too long.
                engine.AssertLogContains("MSB6002");
            }
        }

        /// <summary>
        /// Exercise the code in ToolTask's default implementation of HandleExecutionErrors.
        /// </summary>
        [Fact]
        public void HandleExecutionErrorsWhenToolDoesntLogError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows ? "/C garbagegarbagegarbagegarbage.exe" : "-c garbagegarbagegarbagegarbage.exe";

                t.Execute().ShouldBeFalse();
                t.ExitCode.ShouldBe(NativeMethodsShared.IsWindows ? 1 : 127); // cmd.exe error code is 1, sh error code is 127

                // We just tried to run "cmd.exe /C garbagegarbagegarbagegarbage.exe".  This should fail,
                // but since "cmd.exe" doesn't log its errors in canonical format, no errors got
                // logged by the tool itself.  Therefore, ToolTask's default implementation of
                // HandleTaskExecutionErrors should have logged error MSB6006.
                engine.AssertLogContains("MSB6006");
            }
        }

        /// <summary>
        /// Exercise the code in ToolTask's default implementation of HandleExecutionErrors.
        /// </summary>
        [Fact]
        [Trait("Category", "netcore-osx-failing")]
        [Trait("Category", "netcore-linux-failing")]
        public void HandleExecutionErrorsWhenToolLogsError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C echo Main.cs(17,20): error CS0168: The variable 'foo' is declared but never used"
                                                : @"-c """"""echo Main.cs\(17,20\): error CS0168: The variable 'foo' is declared but never used""""""";

                t.Execute().ShouldBeFalse();

                // The above command logged a canonical error message.  Therefore ToolTask should
                // not log its own error beyond that.
                engine.AssertLogDoesntContain("MSB6006");
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                t.ExitCode.ShouldBe(-1);
                engine.Errors.ShouldBe(1);
            }
        }

        /// <summary>
        /// ToolTask should never run String.Format on strings that are
        /// not meant to be formatted.
        /// </summary>
        [Fact]
        public void DoNotFormatTaskCommandOrMessage()
        {
            using MyTool t = new MyTool();
            MockEngine3 engine = new MockEngine3();
            t.BuildEngine = engine;
            // Unmatched curly would crash if they did
            t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                            ? "/C echo hello world {"
                                            : @"-c ""echo hello world {""";
            t.Execute();
            engine.AssertLogContains("echo hello world {");
            engine.Errors.ShouldBe(0);
        }

        /// <summary>
        /// Process notification encoding should be consistent with console code page.
        /// not meant to be formatted.
        /// </summary>
        [InlineData(0, "")]
        [InlineData(-1, "1>&2")]
        [Theory]
        public void ProcessNotificationEncodingConsistentWithConsoleCodePage(int exitCode, string errorPart)
        {
            using MyTool t = new MyTool();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.UseCommandProcessor = true;
            t.LogStandardErrorAsError = true;
            t.EchoOff = true;
            t.UseUtf8Encoding = EncodingUtilities.UseUtf8Always;
            string content = "Building Custom Rule プロジェクト";
            string outputMessage = exitCode == 0 ? content : $"'{content}' {errorPart}";
            string commandLine = $"echo {outputMessage}";
            t.MockCommandLineCommands = commandLine;
            t.Execute();
            t.ExitCode.ShouldBe(exitCode);

            string log = engine.Log;
            string singleQuote = NativeMethodsShared.IsWindows ? "'" : string.Empty;
            string displayMessage = exitCode == 0 ? content : $"ERROR : {singleQuote}{content}{singleQuote}";
            string pattern = $"{commandLine}{Environment.NewLine}\\s*{displayMessage}";
            Regex regex = new Regex(pattern);
            regex.Matches(log).Count.ShouldBe(1, $"{log} doesn't contain the log matching the pattern: {pattern}");
        }

        /// <summary>
        /// When a message is logged to the standard error stream do not error is LogStandardErrorAsError is not true or set.
        /// </summary>
        [Fact]
        public void DoNotErrorWhenTextSentToStandardError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways' 1>&2"
                                                : @"-c ""echo Who made you king anyways 1>&2""";

                t.Execute().ShouldBeTrue();

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                t.ExitCode.ShouldBe(0);
                engine.Errors.ShouldBe(0);
            }
        }

        /// <summary>
        /// When a message is logged to the standard output stream do not error is LogStandardErrorAsError is  true
        /// </summary>
        [Fact]
        public void DoNotErrorWhenTextSentToStandardOutput()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways'"
                                                : @"-c ""echo Who made you king anyways""";

                t.Execute().ShouldBeTrue();

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                t.ExitCode.ShouldBe(0);
                engine.Errors.ShouldBe(0);
            }
        }

        /// <summary>
        /// When a message is logged to the standard error stream error if LogStandardErrorAsError is true
        /// </summary>
        [Fact]
        public void ErrorWhenTextSentToStandardError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways' 1>&2"
                                                : @"-c ""echo 'Who made you king anyways' 1>&2""";

                t.Execute().ShouldBeFalse();

                engine.AssertLogDoesntContain("MSB3073");
                engine.AssertLogContains("Who made you king anyways");
                t.ExitCode.ShouldBe(-1);
                engine.Errors.ShouldBe(1);
            }
        }


        /// <summary>
        /// When ToolExe is set, it is used instead of ToolName
        /// </summary>
        [Fact]
        public void ToolExeWinsOverToolName()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "c:\\baz\\foo.exe" : "/baz/foo.exe";

                t.ToolExe.ShouldBe("foo.exe");
                t.ToolExe = "bar.exe";
                t.ToolExe.ShouldBe("bar.exe");
            }
        }

        /// <summary>
        /// When ToolExe is set, it is appended to ToolPath instead
        /// of the regular tool name
        /// </summary>
        [Fact]
        public void ToolExeIsFoundOnToolPath()
        {
            string shellName = NativeMethodsShared.IsWindows ? "cmd.exe" : "sh";
            string copyName = NativeMethodsShared.IsWindows ? "xcopy.exe" : "cp";
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.FullToolName = shellName;
                string systemPath = NativeMethodsShared.IsUnixLike ? "/bin" : Environment.GetFolderPath(Environment.SpecialFolder.System);
                t.ToolPath = systemPath;

                t.Execute();
                t.PathToToolUsed.ShouldBe(Path.Combine(systemPath, shellName));
                engine.AssertLogContains(shellName);
                engine.Log = string.Empty;

                t.ToolExe = copyName;
                t.Execute();
                t.PathToToolUsed.ShouldBe(Path.Combine(systemPath, copyName));
                engine.AssertLogContains(copyName);
                engine.AssertLogDoesntContain(shellName);
            }
        }

        /// <summary>
        /// Task is not found on path - regress #499196
        /// </summary>
        [Fact]
        public void TaskNotFoundOnPath()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.FullToolName = "doesnotexist.exe";

                t.Execute().ShouldBeFalse();
                t.ExitCode.ShouldBe(-1);
                engine.Errors.ShouldBe(1);

                // Does not throw an exception
            }
        }

        /// <summary>
        /// Task is found on path.
        /// </summary>
        [Fact]
        public void TaskFoundOnPath()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                string toolName = NativeMethodsShared.IsWindows ? "cmd.exe" : "sh";
                t.FullToolName = toolName;

                t.Execute().ShouldBeTrue();
                t.ExitCode.ShouldBe(0);
                engine.Errors.ShouldBe(0);

                string systemPath = NativeMethodsShared.IsUnixLike ? "/bin" : Environment.GetFolderPath(Environment.SpecialFolder.System);
                engine.AssertLogContains(
                Path.Combine(systemPath, toolName));
            }
        }

        /// <summary>
        /// StandardOutputImportance set to Low should not show up in our log
        /// </summary>
        [Fact]
        public void OverrideStdOutImportanceToLow()
        {
            string tempFile = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "findstr.exe" : "grep";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "Low";

                t.Execute().ShouldBeTrue();
                t.ExitCode.ShouldBe(0);
                engine.Errors.ShouldBe(0);

                engine.AssertLogDoesntContain("hello world");
            }
            File.Delete(tempFile);
        }

        /// <summary>
        /// StandardOutputImportance set to High should show up in our log
        /// </summary>
        [Fact]
        public void OverrideStdOutImportanceToHigh()
        {
            string tempFile = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "findstr.exe" : "grep";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "High";

                t.Execute().ShouldBeTrue();
                t.ExitCode.ShouldBe(0);
                engine.Errors.ShouldBe(0);

                engine.AssertLogContains("hello world");
            }
            File.Delete(tempFile);
        }

        /// <summary>
        /// This is to ensure that somebody could write a task that implements ToolTask,
        /// wraps some .EXE tool, and still have the ability to parse the stdout/stderr
        /// himself.  This is so that in case the tool doesn't log its errors in canonical
        /// format, the task can still opt to do something reasonable with it.
        /// </summary>
        [Fact]
        public void ToolTaskCanChangeCanonicalErrorFormat()
        {
            string tempFile = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(tempFile, @"
                Main.cs(17,20): warning CS0168: The variable 'foo' is declared but never used.
                BADTHINGHAPPENED: This is my custom error format that's not in canonical error format.
                ");

            using (MyTool t = new MyTool())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                // The command we're giving is the command to spew the contents of the temp
                // file we created above.
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? $"/C type \"{tempFile}\""
                                                : $"-c \"cat \'{tempFile}\'\"";

                t.Execute();

                // The above command logged a canonical warning, as well as a custom error.
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                engine.AssertLogContains("BADTHINGHAPPENED");
                engine.AssertLogContains("This is my custom error format");

                engine.Warnings.ShouldBe(1); // "Expected one warning in log."
                engine.Errors.ShouldBe(1); // "Expected one error in log."
            }

            File.Delete(tempFile);
        }

        /// <summary>
        /// Passing env vars through the tooltask public property
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTask()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            string userVarName = NativeMethodsShared.IsWindows ? "username" : "user";
            task.EnvironmentVariables = new[] { "a=b", "c=d", userVarName + "=x" /* built-in */, "path=" /* blank value */};
            bool result = task.Execute();

            result.ShouldBe(true);
            task.ExecuteCalled.ShouldBe(true);

            ProcessStartInfo startInfo = task.StartInfo;

            startInfo.Environment["a"].ShouldBe("b");
            startInfo.Environment["c"].ShouldBe("d");
            startInfo.Environment[userVarName].ShouldBe("x");
            startInfo.Environment["path"].ShouldBe(String.Empty);

            if (NativeMethodsShared.IsWindows)
            {
                Assert.Equal(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        startInfo.Environment["programfiles"],
                        true);
            }
        }

        /// <summary>
        /// Equals sign in value
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskEqualsSign()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = new[] { "a=b=c" };
            bool result = task.Execute();

            result.ShouldBe(true);
            task.StartInfo.Environment["a"].ShouldBe("b=c");
        }

        /// <summary>
        /// No value provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid1()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = new[] { "x" };
            bool result = task.Execute();

            result.ShouldBe(false);
            task.ExecuteCalled.ShouldBe(false);
        }

        /// <summary>
        /// Empty string provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid2()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = new[] { "" };
            bool result = task.Execute();

            result.ShouldBe(false);
            task.ExecuteCalled.ShouldBe(false);
        }

        /// <summary>
        /// Empty name part provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid3()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = new[] { "=a;b=c" };
            bool result = task.Execute();

            result.ShouldBe(false);
            task.ExecuteCalled.ShouldBe(false);
        }

        /// <summary>
        /// Not set should not wipe out other env vars
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskNotSet()
        {
            using MyTool task = new MyTool();
            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = null;
            bool result = task.Execute();

            result.ShouldBe(true);
            task.ExecuteCalled.ShouldBe(true);
            Assert.True(task.StartInfo.Environment["PATH"].Length > 0);
        }

        /// <summary>
        /// Verifies that if a directory with the same name of the tool exists that the tool task correctly
        /// ignores the directory.
        /// </summary>
        [Fact]
        public void ToolPathIsFoundWhenDirectoryExistsWithNameOfTool()
        {
            string toolName = NativeMethodsShared.IsWindows ? "cmd" : "sh";
            string savedCurrentDirectory = Directory.GetCurrentDirectory();

            try
            {
                using (var env = TestEnvironment.Create())
                {
                    string tempDirectory = env.CreateFolder().Path;
                    env.SetCurrentDirectory(tempDirectory);
                    env.SetEnvironmentVariable("PATH", $"{tempDirectory}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}");
                    Directory.SetCurrentDirectory(tempDirectory);

                    string directoryNamedSameAsTool = Directory.CreateDirectory(Path.Combine(tempDirectory, toolName)).FullName;

                    using MyTool task = new MyTool
                    {
                        BuildEngine = new MockEngine3(),
                        FullToolName = toolName,
                    };
                    bool result = task.Execute();

                    Assert.NotEqual(directoryNamedSameAsTool, task.PathToToolUsed);

                    result.ShouldBeTrue();
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(savedCurrentDirectory);
            }
        }

        /// <summary>
        /// Confirms we can find a file on the PATH.
        /// </summary>
        [Fact]
        public void FindOnPathSucceeds()
        {
            string[] expectedCmdPath;
            string shellName;
            string cmdPath;
            if (NativeMethodsShared.IsWindows)
            {
                expectedCmdPath = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe").ToUpperInvariant() };
                shellName = "cmd.exe";
                cmdPath = ToolTask.FindOnPath(shellName).ToUpperInvariant();
            }
            else
            {
                expectedCmdPath = new[] { "/bin/sh", "/usr/bin/sh" };
                shellName = "sh";
                cmdPath = ToolTask.FindOnPath(shellName);
            }

            cmdPath.ShouldBeOneOf(expectedCmdPath);
        }

        /// <summary>
        /// Equals sign in value
        /// </summary>
        [Fact]
        public void GetProcessStartInfoCanOverrideEnvironmentVariables()
        {
            using MyTool task = new MyTool();
            task.DoProcessStartInfoMutation = (p) => p.Environment.Remove("a");

            task.BuildEngine = new MockEngine3();
            task.EnvironmentVariables = new[] { "a=b" };
            bool result = task.Execute();

            result.ShouldBe(true);
            task.StartInfo.Environment.ContainsKey("a").ShouldBe(false);
        }

        [Fact]
        public void VisualBasicLikeEscapedQuotesInCommandAreNotMadeForwardSlashes()
        {
            using MyTool t = new MyTool();
            MockEngine3 engine = new MockEngine3();
            t.BuildEngine = engine;
            t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                            ? "/C echo \"hello \\\"world\\\"\""
                                            : "-c echo \"hello \\\"world\\\"\"";
            t.Execute();
            engine.AssertLogContains("echo \"hello \\\"world\\\"\"");
            engine.Errors.ShouldBe(0);
        }

        private sealed class MyToolWithCustomProcess : MyTool
        {
            protected override Process StartToolProcess(Process proc)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope - caller needs the process
                Process customProcess = new Process();
#pragma warning restore CA2000
                customProcess.StartInfo = proc.StartInfo;

                customProcess.EnableRaisingEvents = true;
                customProcess.Exited += ReceiveExitNotification;

                customProcess.ErrorDataReceived += ReceiveStandardErrorData;
                customProcess.OutputDataReceived += ReceiveStandardOutputData;
                return base.StartToolProcess(customProcess);
            }
        }

        [Fact]
        public void UsesCustomProcess()
        {
            using (MyToolWithCustomProcess t = new MyToolWithCustomProcess())
            {
                MockEngine3 engine = new MockEngine3();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                    ? "/C echo hello_stdout & echo hello_stderr >&2"
                    : "-c \"echo hello_stdout ; echo hello_stderr >&2\"";

                t.Execute();

                engine.AssertLogContains("\nhello_stdout");
                engine.AssertLogContains("\nhello_stderr");
            }
        }

        /// <summary>
        /// Verifies that a ToolTask running under the command processor on Windows has autorun
        /// disabled or enabled depending on an escape hatch.
        /// </summary>
        [Theory]
        [InlineData("MSBUILDUSERAUTORUNINCMD", null, true)]
        [InlineData("MSBUILDUSERAUTORUNINCMD", "0", true)]
        [InlineData("MSBUILDUSERAUTORUNINCMD", "1", false)]
        [Trait("Category", "nonosxtests")]
        [Trait("Category", "nonlinuxtests")]
        public void ExecTaskDisablesAutoRun(string environmentVariableName, string environmentVariableValue, bool autoRunShouldBeDisabled)
        {
            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                testEnvironment.SetEnvironmentVariable(environmentVariableName, environmentVariableValue);

                ToolTaskThatGetsCommandLine task = new ToolTaskThatGetsCommandLine
                {
                    UseCommandProcessor = true
                };

                task.Execute();

                if (autoRunShouldBeDisabled)
                {
                    task.CommandLineCommands.ShouldContain("/D ");
                }
                else
                {
                    task.CommandLineCommands.ShouldNotContain("/D ");
                }
            }
        }

        /// <summary>
        /// A simple implementation of <see cref="ToolTask"/> that allows tests to verify the command-line that was generated.
        /// </summary>
        private sealed class ToolTaskThatGetsCommandLine : ToolTask
        {
            protected override string ToolName => "cmd.exe";

            protected override string GenerateFullPathToTool() => null;

            protected override int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
            {
                PathToTool = pathToTool;
                ResponseFileCommands = responseFileCommands;
                CommandLineCommands = commandLineCommands;

                return 0;
            }
            protected override void LogToolCommand(string message)
            {
            }

            public string CommandLineCommands { get; private set; }

            public string PathToTool { get; private set; }

            public string ResponseFileCommands { get; private set; }
        }

        [Theory]
        [InlineData("MSBUILDAVOIDUNICODE", null, false)]
        [InlineData("MSBUILDAVOIDUNICODE", "0", false)]
        [InlineData("MSBUILDAVOIDUNICODE", "1", true)]
        public void ToolTaskCanUseUnicode(string environmentVariableName, string environmentVariableValue, bool expectNormalizationToANSI)
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create(_output);

            testEnvironment.SetEnvironmentVariable(environmentVariableName, environmentVariableValue);

            var output = testEnvironment.ExpectFile();

            MockEngine3 engine = new MockEngine3();

            var task = new ToolTaskThatNeedsUnicode
            {
                BuildEngine = engine,
                UseCommandProcessor = true,
                OutputPath = output.Path,
            };

            task.Execute();

            File.Exists(output.Path).ShouldBeTrue();
            if (NativeMethodsShared.IsUnixLike // treat all UNIXy OSes as capable of UTF-8 everywhere
                || !expectNormalizationToANSI)
            {
                File.ReadAllText(output.Path).ShouldContain("łoł");
            }
            else
            {
                File.ReadAllText(output.Path).ShouldContain("lol");
            }
        }


        private sealed class ToolTaskThatNeedsUnicode : ToolTask
        {
            protected override string ToolName => "cmd.exe";

            [Required]
            public string OutputPath { get; set; }

            public ToolTaskThatNeedsUnicode()
            {
                UseCommandProcessor = true;
            }

            protected override string GenerateFullPathToTool()
            {
                return "cmd.exe";
            }

            protected override string GenerateCommandLineCommands()
            {
                return $"echo łoł > {OutputPath}";
            }
        }

        /// <summary>
        /// Verifies the validation of the <see cref="ToolTask.TaskProcessTerminationTimeout" />.
        /// </summary>
        /// <param name="timeout">New value for <see cref="ToolTask.TaskProcessTerminationTimeout" />.</param>
        /// <param name="isInvalidValid">Is a task expected to be valid or not.</param>
        [Theory]
        [InlineData(int.MaxValue, false)]
        [InlineData(97, false)]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(-2, true)]
        [InlineData(-101, true)]
        [InlineData(int.MinValue, true)]
        public void SetsTerminationTimeoutCorrectly(int timeout, bool isInvalidValid)
        {
            using var env = TestEnvironment.Create(_output);

            // Task under test:
            var task = new ToolTaskSetsTerminationTimeout
            {
                BuildEngine = new MockEngine3()
            };

            task.TerminationTimeout = timeout;
            task.ValidateParameters().ShouldBe(!isInvalidValid);
            task.TerminationTimeout.ShouldBe(timeout);
        }

		/// <summary>
        /// Verifies that a ToolTask instance can return correct results when executed multiple times with timeout.
        /// </summary>
        /// <param name="repeats">Specifies the number of repeats for external command execution.</param>
        /// <param name="initialDelay">Delay to generate on the first execution in milliseconds.</param>
        /// <param name="followupDelay">Delay to generate on follow-up execution in milliseconds.</param>
        /// <param name="timeout">Task timeout in milliseconds.</param>
        /// <remarks>
        /// These tests execute the same task instance multiple times, which will in turn run a shell command to sleep
        /// predefined amount of time. The first execution may time out, but all following ones won't. It is expected
        /// that all following executions return success.
        /// </remarks>
        [Theory]
        [InlineData(1, 1, 1, -1)] // Normal case, no repeat.
        [InlineData(3, 1, 1, -1)] // Repeat without timeout.
        [InlineData(3, 10000, 1, 1000)] // Repeat with timeout.
        public void ToolTaskThatTimeoutAndRetry(int repeats, int initialDelay, int followupDelay, int timeout)
        {
            using var env = TestEnvironment.Create(_output);

            MockEngine3 engine = new();

            // Task under test:
            var task = new ToolTaskThatSleeps
            {
                BuildEngine = engine,
                InitialDelay = initialDelay,
                FollowupDelay = followupDelay,
                Timeout = timeout
            };

            // Execute the same task instance multiple times. The index is one-based.
            bool result;
            for (int i = 1; i <= repeats; i++)
            {
                // Execute the task:
                result = task.Execute();

                _output.WriteLine(engine.Log);

                task.RepeatCount.ShouldBe(i);

                // The first execution may fail (timeout), but all following ones should succeed:
                if (i > 1)
                {
                    result.ShouldBeTrue();
                    task.ExitCode.ShouldBe(0);
                }
            }
        }

        /// <summary>
        /// A simple implementation of <see cref="ToolTask"/> to sleep for a while.
        /// </summary>
        /// <remarks>
        /// This task runs shell command to sleep for predefined, variable amount of time based on how many times the
        /// instance has been executed.
        /// </remarks>
        private sealed class ToolTaskThatSleeps : ToolTask
        {
            // Windows prompt command to sleep:
            private readonly string _windowsSleep = "/c start /wait timeout {0}";

            // UNIX command to sleep:
            private readonly string _unixSleep = "-c \"sleep {0}\"";

            // Full path to shell:
            private readonly string _pathToShell;

            public ToolTaskThatSleeps()
                : base()
            {
                // Determines shell to use: cmd for Windows, sh for UNIX-like systems:
                _pathToShell = NativeMethodsShared.IsUnixLike ? "/bin/sh" : "cmd.exe";
            }

            /// <summary>
            /// Gets or sets the delay for the first execution.
            /// </summary>
            /// <remarks>
            /// Defaults to 10 seconds.
            /// </remarks>
            public Int32 InitialDelay { get; set; } = 10000;

            /// <summary>
            /// Gets or sets the delay for the follow-up executions.
            /// </summary>
            /// <remarks>
            /// Defaults to 1 milliseconds.
            /// </remarks>
            public Int32 FollowupDelay { get; set; } = 1;

            /// <summary>
            /// Int32 output parameter for the repeat counter for test purpose.
            /// </summary>
            [Output]
            public Int32 RepeatCount { get; private set; } = 0;

            /// <summary>
            /// Gets the tool name (shell).
            /// </summary>
            protected override string ToolName => Path.GetFileName(_pathToShell);

            /// <summary>
            /// Gets the full path to shell.
            /// </summary>
            protected override string GenerateFullPathToTool() => _pathToShell;

            /// <summary>
            /// Generates a shell command to sleep different amount of time based on repeat counter.
            /// </summary>
            protected override string GenerateCommandLineCommands() =>
                NativeMethodsShared.IsUnixLike ?
                string.Format(_unixSleep, RepeatCount < 2 ? InitialDelay / 1000.0 : FollowupDelay / 1000.0) :
                string.Format(_windowsSleep, RepeatCount < 2 ? InitialDelay / 1000.0 : FollowupDelay / 1000.0);

            /// <summary>
            /// Ensures that test parameters make sense.
            /// </summary>
            protected internal override bool ValidateParameters() =>
                (InitialDelay > 0) && (FollowupDelay > 0) && base.ValidateParameters();

            /// <summary>
            /// Runs shell command to sleep for a while.
            /// </summary>
            /// <returns>
            /// true if the task runs successfully; false otherwise.
            /// </returns>
            public override bool Execute()
            {
                RepeatCount++;
                return base.Execute();
            }
        }

        /// <summary>
        /// A simple implementation of <see cref="ToolTask"/> to excercise <see cref="ToolTask.TaskProcessTerminationTimeout" />.
        /// </summary>
        private sealed class ToolTaskSetsTerminationTimeout : ToolTask
        {
            public ToolTaskSetsTerminationTimeout()
                : base()
            {
                base.TaskResources = AssemblyResources.PrimaryResources;
            }

            /// <summary>
            /// Gets or sets <see cref="ToolTask.TaskProcessTerminationTimeout" />.
            /// </summary>
            /// <remarks>
            /// This is just a proxy property to access <see cref="ToolTask.TaskProcessTerminationTimeout" />.
            /// </remarks>
            public int TerminationTimeout
            {
                get => TaskProcessTerminationTimeout;
                set => TaskProcessTerminationTimeout = value;
            }

            /// <summary>
            /// Gets the tool name (dummy).
            /// </summary>
            protected override string ToolName => string.Empty;

            /// <summary>
            /// Gets the full path to tool (dummy).
            /// </summary>
            protected override string GenerateFullPathToTool() => string.Empty;

            /// <summary>
            /// Does nothing.
            /// </summary>
            /// <returns>
            /// Always returns true.
            /// </returns>
            /// <remarks>
            /// This dummy tool task is not meant to run anything.
            /// </remarks>
            public override bool Execute() => true;
        }
    }
}
