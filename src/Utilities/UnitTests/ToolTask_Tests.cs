// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
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
                _fullToolName =
                    Path.Combine
                    (
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "cmd.exe"
                    );
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
                StartInfo = base.GetProcessStartInfo(GenerateFullPathToTool(), "/x", null);
                return result;
            }
        };

        [Fact]
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

        [Fact]
        public void Regress_Mutation_MissingExecutableIsLogged()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.ToolPath = @"C:\MyAlternatePath";

                Assert.False(t.Execute());

                // There should be an error about invalid task location.
                engine.AssertLogContains("MSB6004");
            }
        }

        [Fact]
        public void Regress_Mutation_WarnIfCommandLineTooLong()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                // "cmd.exe" croaks big-time when given a very long command-line.  It pops up a message box on
                // Windows XP.  We can't have that!  So use "attrib.exe" for this exercise instead.
                t.FullToolName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "attrib.exe");

                t.MockCommandLineCommands = new String('x', 32001);

                // It's only a warning, we still succeed
                Assert.True(t.Execute());
                Assert.Equal(0, t.ExitCode);
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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = "/C garbagegarbagegarbagegarbage.exe";

                Assert.False(t.Execute());
                Assert.Equal(1, t.ExitCode); // cmd.exe error code is 1

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
        public void HandleExecutionErrorsWhenToolLogsError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = "/C echo Main.cs(17,20): error CS0168: The variable 'foo' is declared but never used";

                Assert.False(t.Execute());

                // The above command logged a canonical error message.  Therefore ToolTask should
                // not log its own error beyond that.
                engine.AssertLogDoesntContain("MSB6006");
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                Assert.Equal(-1, t.ExitCode);
                Assert.Equal(1, engine.Errors);
            }
        }

        /// <summary>
        /// ToolTask should never run String.Format on strings that are 
        /// not meant to be formatted.
        /// </summary>
        [Fact]
        public void DoNotFormatTaskCommandOrMessage()
        {
            MyTool t = new MyTool();
            MockEngine engine = new MockEngine();
            t.BuildEngine = engine;
            t.MockCommandLineCommands = "/C echo hello world {"; // Unmatched curly would crash if they did
            t.Execute();
            engine.AssertLogContains("echo hello world {");
            Assert.Equal(0, engine.Errors);
        }

        /// <summary>
        /// When a message is logged to the standard error stream do not error is LogStandardErrorAsError is not true or set.
        /// </summary>
        [Fact]
        public void DoNotErrorWhenTextSentToStandardError()
        {
            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.MockCommandLineCommands = "/C Echo 'Who made you king anyways' 1>&2";

                Assert.True(t.Execute());

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                Assert.Equal(0, t.ExitCode);
                Assert.Equal(0, engine.Errors);
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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = "/C Echo 'Who made you king anyways'";

                Assert.True(t.Execute());

                engine.AssertLogDoesntContain("MSB");
                engine.AssertLogContains("Who made you king anyways");
                Assert.Equal(0, t.ExitCode);
                Assert.Equal(0, engine.Errors);
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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.LogStandardErrorAsError = true;
                t.MockCommandLineCommands = "/C Echo 'Who made you king anyways' 1>&2";

                Assert.False(t.Execute());

                engine.AssertLogDoesntContain("MSB3073");
                engine.AssertLogContains("Who made you king anyways");
                Assert.Equal(-1, t.ExitCode);
                Assert.Equal(1, engine.Errors);
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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = "c:\\baz\\foo.exe";

                Assert.Equal("foo.exe", t.ToolExe);
                t.ToolExe = "bar.exe";
                Assert.Equal("bar.exe", t.ToolExe);
            }
        }

        /// <summary>
        /// When ToolExe is set, it is appended to ToolPath instead
        /// of the regular tool name
        /// </summary>
        [Fact]
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
                Assert.Equal(Path.Combine(systemPath, "cmd.exe"), t.PathToToolUsed);
                engine.AssertLogContains("cmd.exe");
                engine.Log = String.Empty;

                t.ToolExe = "xcopy.exe";
                t.Execute();
                Assert.Equal(Path.Combine(systemPath, "xcopy.exe"), t.PathToToolUsed);
                engine.AssertLogContains("xcopy.exe");
                engine.AssertLogDoesntContain("cmd.exe");
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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = "doesnotexist.exe";

                Assert.False(t.Execute());
                Assert.Equal(-1, t.ExitCode);
                Assert.Equal(1, engine.Errors);

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
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;
                t.FullToolName = "cmd.exe";

                Assert.True(t.Execute());
                Assert.Equal(0, t.ExitCode);
                Assert.Equal(0, engine.Errors);

                engine.AssertLogContains(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"));
            }
        }

        /// <summary>
        /// StandardOutputImportance set to Low should now show up in our log
        /// </summary>
        [Fact]
        public void OverrideStdOutImportanceToLow()
        {
            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = "find.exe";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "Low";

                Assert.True(t.Execute());
                Assert.Equal(0, t.ExitCode);
                Assert.Equal(0, engine.Errors);

                engine.AssertLogDoesntContain("hello world");
            }
            File.Delete(tempFile);
        }

        /// <summary>
        /// StandardOutputImportance set to Low should now show up in our log
        /// </summary>
        [Fact]
        public void OverrideStdOutImportanceToHigh()
        {
            string tempFile = FileUtilities.GetTemporaryFile();
            File.WriteAllText(tempFile, @"hello world");

            using (MyTool t = new MyTool())
            {
                MockEngine engine = new MockEngine();
                engine.MinimumMessageImportance = MessageImportance.High;

                t.BuildEngine = engine;
                t.FullToolName = "find.exe";
                t.MockCommandLineCommands = "\"hello\" \"" + tempFile + "\"";
                t.StandardOutputImportance = "High";

                Assert.True(t.Execute());
                Assert.Equal(0, t.ExitCode);
                Assert.Equal(0, engine.Errors);

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
                t.MockCommandLineCommands = "/C type \"" + tempFile + "\"";

                t.Execute();

                // The above command logged a canonical warning, as well as a custom error.
                engine.AssertLogContains("CS0168");
                engine.AssertLogContains("The variable 'foo' is declared but never used");
                engine.AssertLogContains("BADTHINGHAPPENED");
                engine.AssertLogContains("This is my custom error format");

                Assert.Equal(1, engine.Warnings); // "Expected one warning in log."
                Assert.Equal(1, engine.Errors); // "Expected one error in log."
            }

            File.Delete(tempFile);
        }

        /// <summary>
        /// Passing env vars through the tooltask public property
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTask()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "a=b", "c=d", "username=x" /* built-in */, "path=" /* blank value */};
            bool result = task.Execute();

            Assert.Equal(true, result);
            Assert.Equal(true, task.ExecuteCalled);

            ProcessStartInfo startInfo = task.StartInfo;

            Assert.Equal("b", startInfo.EnvironmentVariables["a"]);
            Assert.Equal("d", startInfo.EnvironmentVariables["c"]);
            Assert.Equal("x", startInfo.EnvironmentVariables["username"]);
            Assert.Equal(String.Empty, startInfo.EnvironmentVariables["path"]);
            Assert.True(String.Equals(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), startInfo.EnvironmentVariables["programfiles"], StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Equals sign in value
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskEqualsSign()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "a=b=c" };
            bool result = task.Execute();

            Assert.Equal(true, result);
            Assert.Equal("b=c", task.StartInfo.EnvironmentVariables["a"]);
        }

        /// <summary>
        /// No value provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid1()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "x" };
            bool result = task.Execute();

            Assert.Equal(false, result);
            Assert.Equal(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Empty string provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid2()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "" };
            bool result = task.Execute();

            Assert.Equal(false, result);
            Assert.Equal(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Empty name part provided
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskInvalid3()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = new string[] { "=a;b=c" };
            bool result = task.Execute();

            Assert.Equal(false, result);
            Assert.Equal(false, task.ExecuteCalled);
        }

        /// <summary>
        /// Not set should not wipe out other env vars
        /// </summary>
        [Fact]
        public void EnvironmentVariablesToToolTaskNotSet()
        {
            MyTool task = new MyTool();
            task.BuildEngine = new MockEngine();
            task.EnvironmentVariables = null;
            bool result = task.Execute();

            Assert.Equal(true, result);
            Assert.Equal(true, task.ExecuteCalled);
            Assert.Equal(true, task.StartInfo.EnvironmentVariables["username"].Length > 0);
        }
    }
}
