// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    sealed public class ToolTask_Tests
    {
        internal class MyTool : ToolTask, IDisposable
        {
            private string _fullToolName;
            private string _responseFileCommands = String.Empty;
            private string _commandLineCommands = String.Empty;
            private string _pathToToolUsed;

            public MyTool()
                : base()
            {
                _fullToolName = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    NativeMethodsShared.IsUnixLike ? "/bin/sh" : "cmd.exe");
            }

            public void Dispose()
            {
            }

            public string PathToToolUsed
            {
                get { return _pathToToolUsed; }
            }

            public string MockResponseFileCommands
            {
                set { _responseFileCommands = value; }
            }

            public string MockCommandLineCommands
            {
                set { _commandLineCommands = value; }
            }

            public string FullToolName
            {
                set { _fullToolName = value; }
            }

            /// <summary>
            /// Intercepted start info
            /// </summary>
            internal ProcessStartInfo StartInfo
            {
                get;
                private set;
            }

            /// <summary>
            /// Whether execute was called
            /// </summary>
            internal bool ExecuteCalled
            {
                get;
                private set;
            }

            protected override string ToolName
            {
                get { return Path.GetFileName(_fullToolName); }
            }

            protected override string GenerateFullPathToTool()
            {
                return _fullToolName;
            }

            override protected string GenerateResponseFileCommands()
            {
                return _responseFileCommands;
            }

            override protected string GenerateCommandLineCommands()
            {
                // Default is nothing. This is useful for tools where all the parameters can go into a response file.
                return _commandLineCommands;
            }

            override protected void LogEventsFromTextOutput
                (
                string singleLine,
                MessageImportance messageImportance
                )
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

            override protected int ExecuteTool(string pathToTool, string responseFileCommands, string commandLineCommands)
            {
                Console.WriteLine("executetool");
                _pathToToolUsed = pathToTool;
                ExecuteCalled = true;
                int result = base.ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
                StartInfo = base.GetProcessStartInfo(
                    GenerateFullPathToTool(),
                    NativeMethodsShared.IsWindows ? "/x" : string.Empty,
                    null);
                return result;
            }
        };

        [Test]
        public void Regress_Mutation_UserSuppliedToolPathIsLogged()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.ToolPath = @"C:\MyAlternatePath";

                t.Execute();

                // The alternate path should be mentioned in the log.
                engine.AssertLogContains("MyAlternatePath");
            }
        }

        [Test]
        public void Regress_Mutation_MissingExecutableIsLogged()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.ToolPath = @"C:\MyAlternatePath";

                Assert.IsFalse(t.Execute());

                // There should be an error about invalid task location.
                engine.AssertLogContains("MSB6004");
            }
        }

        [Test]
        public void Regress_Mutation_WarnIfCommandLineTooLong()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                // "cmd.exe" croaks big-time when given a very long command-line.  It pops up a message box on
                // Windows XP.  We can't have that!  So use "attrib.exe" for this exercise instead.
                t.FullToolName = NativeMethodsShared.IsWindows ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "attrib.exe") :
                    "/bin/ps";

                t.MockCommandLineCommands = new String('x', 32001);

                // It's only a warning, we still succeed
                Assert.IsTrue(t.Execute());
                Assert.AreEqual(0, t.ExitCode);
                // There should be a warning about the command-line being too long.
                engine.AssertLogContains("MSB6002");
            }
        }

        /// <summary>
        /// Exercise the code in ToolTask's default implementation of HandleExecutionErrors.
        /// </summary>
        [Test]
        public void HandleExecutionErrorsWhenToolDoesntLogError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = "/C garbagegarbagegarbagegarbage.exe";

                Assert.IsFalse(t.Execute());
                Assert.AreEqual(NativeMethodsShared.IsWindows ? 1 : 127, t.ExitCode); // cmd.exe error code is 1, sh error code is 127

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
        [Test]
        public void HandleExecutionErrorsWhenToolLogsError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C echo Main.cs(17,20): error CS0168: The variable 'foo' is declared but never used"
                    : @"-c ""echo 'Main.cs(17,20): error CS0168: The variable \'foo\' is declared but never used'""";

                Assert.IsFalse(t.Execute());

                // The above command logged a canonical error message.  Therefore ToolTask should
                // not log its own error beyond that.
                engine.AssertLogDoesntContain("MSB6006");
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                Assert.AreEqual(-1, t.ExitCode);
                Assert.AreEqual(1, engine.Errors);
            }
        }

        /// <summary>
        /// ToolTask should never run String.Format on strings that are 
        /// not meant to be formatted.
        /// </summary>
        [Test]
        public void DoNotFormatTaskCommandOrMessage()
        {
            MyTool t = new MyTool();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            // Unmatched curly would crash if they did
            t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                            ? "/C echo hello world {"
                                            : "-c echo hello world {";
            t.Execute();
            engine.AssertLogContains("echo hello world {");
            Assert.AreEqual(0, engine.Errors);
        }

        /// <summary>
        /// When a message is logged to the standard error stream do not error is LogStandardErrorAsError is not true or set.
        /// </summary>
        [Test]
        public void DoNotErrorWhenTextSentToStandardError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways' 1>&2"
                                                : "-c 'echo Who made you king anyways' 1>&2";

                Assert.IsTrue(t.Execute());

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                Assert.AreEqual(0, t.ExitCode);
                Assert.AreEqual(0, engine.Errors);
            }
        }

        /// <summary>
        /// When a message is logged to the standard output stream do not error is LogStandardErrorAsError is  true
        /// </summary>
        [Test]
        public void DoNotErrorWhenTextSentToStandardOutput()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways'"
                                                : "-c 'echo Who made you king anyways'";

                Assert.IsTrue(t.Execute());

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                Assert.AreEqual(0, t.ExitCode);
                Assert.AreEqual(0, engine.Errors);
            }
        }

        /// <summary>
        /// When a message is logged to the standard error stream error if LogStandardErrorAsError is true
        /// </summary>
        [Test]
        public void ErrorWhenTextSentToStandardError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? "/C Echo 'Who made you king anyways' 1>&2"
                                                : "-c 'echo Who made you king anyways' 1>&2";

                Assert.IsFalse(t.Execute());

                engine.AssertLogDoesntContain("MSB3073");
                engine.AssertLogContains("Who made you king anyways");
                Assert.AreEqual(-1, t.ExitCode);
                Assert.AreEqual(1, engine.Errors);
            }
        }


        /// <summary>
        /// When ToolExe is set, it is used instead of ToolName
        /// </summary>
        [Test]
        public void ToolExeWinsOverToolName()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "c:\\baz\\foo.exe" : "/baz/foo.exe";

                Assert.AreEqual("foo.exe", t.ToolExe);
                t.ToolExe = "bar.exe";
                Assert.AreEqual("bar.exe", t.ToolExe);
            }
        }

        /// <summary>
        /// When ToolExe is set, it is appended to ToolPath instead
        /// of the regular tool name
        /// </summary>
        [Test]
        public void ToolExeIsFoundOnToolPath()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = "cmd.exe";
                string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                t.ToolPath = systemPath;

                t.Execute();
                Assert.AreEqual(Path.Combine(systemPath, "cmd.exe"), t.PathToToolUsed);
                engine.AssertLogContains("cmd.exe");
                engine.Log = String.Empty;

                t.ToolExe = "xcopy.exe";
                t.Execute();
                Assert.AreEqual(Path.Combine(systemPath, "xcopy.exe"), t.PathToToolUsed);
                engine.AssertLogContains("xcopy.exe");
                engine.AssertLogDoesntContain("cmd.exe");
            }
        }

        /// <summary>
        /// Task is not found on path - regress #499196
        /// </summary>
        [Test]
        public void TaskNotFoundOnPath()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = "doesnotexist.exe";

                Assert.IsFalse(t.Execute());
                Assert.AreEqual(-1, t.ExitCode);
                Assert.AreEqual(1, engine.Errors);

                // Does not throw an exception
            }
        }

        /// <summary>
        /// Task is found on path.
        /// </summary>
        [Test]
        public void TaskFoundOnPath()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                string toolName = NativeMethodsShared.IsWindows ? "cmd.exe" : "/bin/sh";
                t.FullToolName = toolName;

                Assert.IsTrue(t.Execute());
                Assert.AreEqual(0, t.ExitCode);
                Assert.AreEqual(0, engine.Errors);

                engine.AssertLogContains(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), toolName));
            }
        }

        /// <summary>
        /// StandardOutputImportance set to Low should now show up in our log
        /// </summary>
        [Test]
        public void OverrideStdOutImportanceToLow()
        {
            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "find.exe" : "grep";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "Low";

                Assert.IsTrue(t.Execute());
                Assert.AreEqual(0, t.ExitCode);
                Assert.AreEqual(0, engine.Errors);

                engine.AssertLogDoesntContain("hello world");
            }
            File.Delete(tempFile);
        }

        /// <summary>
        /// StandardOutputImportance set to Low should now show up in our log
        /// </summary>
        [Test]
        public void OverrideStdOutImportanceToHigh()
        {
            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = NativeMethodsShared.IsWindows ? "find.exe" : "grep";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "High";

                Assert.IsTrue(t.Execute());
                Assert.AreEqual(0, t.ExitCode);
                Assert.AreEqual(0, engine.Errors);

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
        [Test]
        public void ToolTaskCanChangeCanonicalErrorFormat()
        {
            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, @"
                Main.cs(17,20): warning CS0168: The variable 'foo' is declared but never used.
                BADTHINGHAPPENED: This is my custom error format that's not in canonical error format.
                ");

            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                // The command we're giving is the command to spew the contents of the temp
                // file we created above.
                t.MockCommandLineCommands = NativeMethodsShared.IsWindows
                                                ? ("/C type \"" + tempFile + "\"")
                                                : ("-c 'cat \"" + tempFile + "\"'");

                t.Execute();

                // The above command logged a canonical warning, as well as a custom error.
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                engine.AssertLogContains("BADTHINGHAPPENED");
                engine.AssertLogContains("This is my custom error format");

                Assert.AreEqual(1, engine.Warnings, "Expected one warning in log.");
                Assert.AreEqual(1, engine.Errors, "Expected one error in log.");
            }

            File.Delete(tempFile);
        }

        /// <summary>
        /// Passing env vars through the tooltask public property
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTask()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            string userVarName = NativeMethodsShared.IsWindows ? "username" : "user";
            task.EnvironmentVariables = new string[] { "a=b", "c=d", userVarName + "=x" /* built-in */, "path=" /* blank value */};
            bool result = task.Execute();

            Assert.AreEqual(true, result);
            Assert.AreEqual(true, task.ExecuteCalled);

            ProcessStartInfo startInfo = task.StartInfo;

            Assert.AreEqual("b", startInfo.EnvironmentVariables["a"]);
            Assert.AreEqual("d", startInfo.EnvironmentVariables["c"]);
            Assert.AreEqual("x", startInfo.EnvironmentVariables[userVarName]);
            Assert.AreEqual(String.Empty, startInfo.EnvironmentVariables["path"]);
            if (NativeMethodsShared.IsWindows)
            {
                Assert.IsTrue(
                    String.Equals(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        startInfo.EnvironmentVariables["programfiles"],
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Equals sign in value
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTaskEqualsSign()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "a=b=c" };
            bool result = task.Execute();

            Assert.AreEqual(true, result);
            Assert.AreEqual("b=c", task.StartInfo.EnvironmentVariables["a"]);
        }

        /// <summary>
        /// No value provided
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTaskInvalid1()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "x" };
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Empty string provided
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTaskInvalid2()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "" };
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Empty name part provided
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTaskInvalid3()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "=a;b=c" };
            bool result = task.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Not set should not wipe out other env vars
        /// </summary>
        [Test]
        public void EnvironmentVariablesToToolTaskNotSet()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = null;
            bool result = task.Execute();

            Assert.AreEqual(true, result);
            Assert.AreEqual(true, task.ExecuteCalled);
            Assert.AreEqual(
                true,
                task.StartInfo.EnvironmentVariables[NativeMethodsShared.IsWindows ? "username" : "USER"].Length > 0);
        }
    }
}
