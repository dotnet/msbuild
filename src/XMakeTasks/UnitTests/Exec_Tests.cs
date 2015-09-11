// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the Exec task
    /// </summary>
    sealed public class Exec_Tests
    {
        private Exec PrepareExec(string command)
        {
            IBuildEngine2 mockEngine = new MockEngine(true);
            Exec exec = new Exec();
            exec.BuildEngine = mockEngine;
            exec.Command = command;
            return exec;
        }

        private ExecWrapper PrepareExecWrapper(string command)
        {
            IBuildEngine2 mockEngine = new MockEngine(true);
            ExecWrapper exec = new ExecWrapper();
            exec.BuildEngine = mockEngine;
            exec.Command = command;
            return exec;
        }

        /// <summary>
        /// Ensures that calling the Exec task does not leave any extra TEMP files
        /// lying around.
        /// </summary>
        [Fact]
        public void NoTempFileLeaks()
        {
            // Get a count of how many temp files there are right now.
            string tempPath = Path.GetTempPath();
            string[] tempFiles = Directory.GetFiles(tempPath);
            int originalTempFileCount = tempFiles.Length;

            // Now run the Exec task on a simple command.
            Exec exec = PrepareExec("echo Four days 'till ZBB!");
            bool result = exec.Execute();

            // Get the new count of temp files.
            tempFiles = Directory.GetFiles(tempPath);
            int newTempFileCount = tempFiles.Length;

            // Ensure that Exec succeeded.
            Assert.True(result);
            // Ensure the new temp file count equals the old temp file count.
            Assert.Equal(originalTempFileCount, newTempFileCount);
        }

        [Fact]
        public void ExitCodeCausesFailure()
        {
            Exec exec = PrepareExec("xcopy thisisanonexistentfile");
            bool result = exec.Execute();

            Assert.Equal(false, result);
            Assert.Equal(4, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3073");
        }

        [Fact(Skip = "Ignored in MSTest")]
        public void Timeout()
        {
            Exec exec = PrepareExec(":foo \n goto foo");
            exec.Timeout = 5;
            bool result = exec.Execute();

            Assert.Equal(false, result);
            Assert.Equal(-1, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB5002");
            Assert.Equal(1, ((MockEngine)exec.BuildEngine).Warnings);
            Assert.Equal(1, ((MockEngine)exec.BuildEngine).Errors);
        }

        [Fact]
        public void ExitCodeGetter()
        {
            Exec exec = PrepareExec("exit 666");
            bool result = exec.Execute();

            Assert.Equal(666, exec.ExitCode);
        }

        [Fact]
        public void LoggedErrorsCauseFailureDespiteExitCode0()
        {
            // This will return 0 exit code, but emitted a canonical error
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror");
            bool result = exec.Execute();

            Assert.Equal(false, result);
            // Exitcode is set to -1
            Assert.Equal(-1, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3073");
        }

        [Fact]
        public void IgnoreExitCodeTrueMakesTaskSucceedDespiteLoggingErrors()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror");
            exec.IgnoreExitCode = true;
            bool result = exec.Execute();

            Assert.Equal(true, result);
        }

        [Fact]
        public void IgnoreExitCodeTrueMakesTaskSucceedDespiteExitCode1()
        {
            Exec exec = PrepareExec("dir ||invalid||");
            exec.IgnoreExitCode = true;
            bool result = exec.Execute();

            Assert.Equal(true, result);
        }

        [Fact]
        public void NonUNCWorkingDirectoryUsed()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            string working = Environment.GetFolderPath(Environment.SpecialFolder.Windows); // not desktop etc - IT redirection messes it up
            exec.WorkingDirectory = working;
            bool result = exec.Execute();

            Assert.Equal(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("[" + working + "]");
        }

        [Fact]
        public void UNCWorkingDirectoryUsed()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            string working = @"\\" + Environment.MachineName + @"\c$";
            exec.WorkingDirectory = working;
            bool result = exec.ValidateParametersAccessor();

            Assert.Equal(true, result);
            Assert.Equal(true, exec.workingDirectoryIsUNC);
            Assert.Equal(working, exec.WorkingDirectory);
            // Should give ToolTask the system folder as the working directory, when it's a UNC
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            Assert.Equal(system, exec.GetWorkingDirectoryAccessor());
        }

        [Fact]
        public void NoWorkingDirectorySet()
        {
            var cd = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

                Exec exec = PrepareExec("echo [%cd%]");
                bool result = exec.Execute();

                string expected = Directory.GetCurrentDirectory();
                Assert.Equal(true, result);
                ((MockEngine)exec.BuildEngine).AssertLogContains("[" + expected + "]");
            }
            finally
            {
                Directory.SetCurrentDirectory(cd);
            }
        }

        /// <summary>
        /// Tests that Exec still executes properly when there's an '&' in the temp directory path
        /// </summary>
        [Fact]
        public void TempPathContainsAmpersand1()
        {
            string directoryWithAmpersand = "foo&bar";
            string newTmp = Path.Combine(Path.GetTempPath(), directoryWithAmpersand);
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Directory.CreateDirectory(newTmp);
                Environment.SetEnvironmentVariable("TMP", newTmp);
                Exec exec = PrepareExec("echo [hello]");

                Assert.True(exec.Execute()); // "Task should have succeeded"
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                if (Directory.Exists(newTmp)) Directory.Delete(newTmp);
            }
        }

        /// <summary>
        /// Tests that Exec still executes properly when there's an ' &' in the temp directory path
        /// </summary>
        [Fact]
        public void TempPathContainsAmpersand2()
        {
            string directoryWithAmpersand = "foo &bar";
            string newTmp = Path.Combine(Path.GetTempPath(), directoryWithAmpersand);
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Directory.CreateDirectory(newTmp);
                Environment.SetEnvironmentVariable("TMP", newTmp);
                Exec exec = PrepareExec("echo [hello]");

                bool taskSucceeded = exec.Execute();
                Assert.True(taskSucceeded); // "Task should have succeeded"
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                if (Directory.Exists(newTmp)) Directory.Delete(newTmp);
            }
        }

        /// <summary>
        /// Tests that Exec still executes properly when there's an '& ' in the temp directory path
        /// </summary>
        [Fact]
        public void TempPathContainsAmpersand3()
        {
            string directoryWithAmpersand = "foo& bar";
            string newTmp = Path.Combine(Path.GetTempPath(), directoryWithAmpersand);
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Directory.CreateDirectory(newTmp);
                Environment.SetEnvironmentVariable("TMP", newTmp);
                Exec exec = PrepareExec("echo [hello]");

                Assert.True(exec.Execute()); // "Task should have succeeded"
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                if (Directory.Exists(newTmp)) Directory.Delete(newTmp);
            }
        }

        /// <summary>
        /// Tests that Exec still executes properly when there's an ' & ' in the temp directory path
        /// </summary>
        [Fact]
        public void TempPathContainsAmpersand4()
        {
            string directoryWithAmpersand = "foo & bar";
            string newTmp = Path.Combine(Path.GetTempPath(), directoryWithAmpersand);
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Directory.CreateDirectory(newTmp);
                Environment.SetEnvironmentVariable("TMP", newTmp);
                Exec exec = PrepareExec("echo [hello]");

                Assert.True(exec.Execute()); // "Task should have succeeded"
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                if (Directory.Exists(newTmp)) Directory.Delete(newTmp);
            }
        }

        /// <summary>
        /// Tests that Exec still executes properly when there's a non-ansi character in the command
        /// </summary>
        [Fact]
        public void ExecTaskUnicodeCharacterInCommand()
        {
            string nonAnsiCharacters = "\u521B\u5EFA";
            string folder = Path.Combine(Path.GetTempPath(), nonAnsiCharacters);
            string command = Path.Combine(folder, "test.cmd");

            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(command, "echo [hello]");
                Exec exec = PrepareExec(command);

                Assert.True(exec.Execute()); // "Task should have succeeded"
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void InvalidUncDirectorySet()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = @"\\thiscomputerdoesnotexistxyz\thiscomputerdoesnotexistxyz";
            bool result = exec.Execute();

            Assert.Equal(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB6003");
        }

        [Fact]
        public void InvalidWorkingDirectorySet()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = @"||invalid||";
            bool result = exec.Execute();

            Assert.Equal(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB6003");
        }

        [Fact]
        public void BogusCustomRegexesCauseOneErrorEach()
        {
            Exec exec = PrepareExec("echo Some output & echo Some output & echo Some output & echo Some output ");
            exec.CustomErrorRegularExpression = "~!@#$%^_)(*&^%$#@@#XF &%^%T$REd((((([[[[";
            exec.CustomWarningRegularExpression = "*";
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.Equal(3, e.Errors);
            e.AssertLogContains("MSB3076");
        }

        [Fact]
        public void CustomErrorRegexSupplied()
        {
            Exec exec = PrepareExec("echo Some output & echo ALERT:This is an error & echo Some more output");
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.Equal(0, e.Errors);
            e.AssertLogContains("ALERT:This is an error");

            exec = PrepareExec("echo Some output & echo ALERT:This is an error & echo Some more output");
            exec.CustomErrorRegularExpression = ".*ALERT.*";
            result = exec.Execute();

            e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.Equal(2, e.Errors);
            e.AssertLogContains("ALERT:This is an error");
        }

        [Fact]
        public void CustomWarningRegexSupplied()
        {
            Exec exec = PrepareExec("echo Some output & echo YOOHOO:This is a warning & echo Some more output");
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.Equal(0, e.Errors);
            Assert.Equal(0, e.Warnings);
            e.AssertLogContains("YOOHOO:This is a warning");

            exec = PrepareExec("echo Some output & echo YOOHOO:This is a warning & echo Some more output");
            exec.CustomWarningRegularExpression = ".*YOOHOO.*";
            result = exec.Execute();

            e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.Equal(0, e.Errors);
            Assert.Equal(1, e.Warnings);
            e.AssertLogContains("YOOHOO:This is a warning");
        }

        [Fact]
        public void ErrorsAndWarningsWithIgnoreStandardErrorWarningFormatTrue()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning");
            exec.IgnoreStandardErrorWarningFormat = true;
            bool result = exec.Execute();

            Assert.Equal(true, result);
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [Fact]
        public void CustomAndStandardErrorsAndWarnings()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning & echo YOGI & echo BEAR & echo some content");
            exec.CustomWarningRegularExpression = ".*BEAR.*";
            exec.CustomErrorRegularExpression = ".*YOGI.*";
            bool result = exec.Execute();

            Assert.Equal(false, result);
            Assert.Equal(3, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(2, ((MockEngine)exec.BuildEngine).Warnings);
        }

        /// <summary>
        /// Nobody should try to run a string emitted from the task through String.Format.
        /// Firstly that's unnecessary and secondly if there's eg an unmatched curly it will throw.
        /// </summary>
        [Fact]
        public void DoNotAttemptToFormatTaskOutput()
        {
            Exec exec = PrepareExec("echo unmatched curly {");
            bool result = exec.Execute();

            Assert.Equal(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("unmatched curly {");
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        /// <summary>
        /// Nobody should try to run a string emitted from the task through String.Format.
        /// Firstly that's unnecessary and secondly if there's eg an unmatched curly it will throw.
        /// </summary>
        [Fact]
        public void DoNotAttemptToFormatTaskOutput2()
        {
            Exec exec = PrepareExec("echo unmatched curly {");
            exec.IgnoreStandardErrorWarningFormat = true;
            bool result = exec.Execute();

            Assert.Equal(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("unmatched curly {");
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [Fact]
        public void NoDuplicateMessagesWhenCustomRegexAndRegularRegexBothMatch()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning ");
            exec.CustomErrorRegularExpression = ".*canonicale.*";
            exec.CustomWarningRegularExpression = ".*canonicalw.*";
            bool result = exec.Execute();

            Assert.Equal(false, result);
            Assert.Equal(2, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(1, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [Fact]
        public void OnlySingleErrorWhenCustomWarningAndCustomErrorRegexesBothMatch()
        {
            Exec exec = PrepareExec("echo YOGI BEAR ");
            exec.CustomErrorRegularExpression = ".*YOGI.*";
            exec.CustomWarningRegularExpression = ".*BEAR.*";
            bool result = exec.Execute();

            Assert.Equal(false, result);
            Assert.Equal(2, ((MockEngine)exec.BuildEngine).Errors);
            Assert.Equal(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [Fact]
        public void GettersSetters()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = "foo";
            Assert.Equal("foo", exec.WorkingDirectory);
            exec.IgnoreExitCode = true;
            Assert.Equal(true, exec.IgnoreExitCode);
            exec.Outputs = null;
            Assert.Equal(0, exec.Outputs.Length);

            ITaskItem[] items = new TaskItem[] { new TaskItem("hi"), new TaskItem("ho") };
            exec.Outputs = items;
            Assert.Equal(items, exec.Outputs);
        }

        [Fact]
        public void StdEncodings()
        {
            ExecWrapper exec = PrepareExecWrapper("echo [%cd%]");

            exec.StdErrEncoding = "US-ASCII";
            Assert.Equal(true, exec.StdErrEncoding.Contains("US-ASCII"));
            Assert.Equal(true, exec.StdErrorEncoding.EncodingName.Contains("US-ASCII"));

            exec.StdOutEncoding = "US-ASCII";
            Assert.Equal(true, exec.StdOutEncoding.Contains("US-ASCII"));
            Assert.Equal(true, exec.StdOutputEncoding.EncodingName.Contains("US-ASCII"));
        }

        [Fact]
        public void AnyExistingEnvVarCalledErrorLevelIsIgnored()
        {
            string oldValue = Environment.GetEnvironmentVariable("errorlevel");

            try
            {
                Exec exec = PrepareExec("echo this is an innocuous successful command");
                Environment.SetEnvironmentVariable("errorlevel", "1");
                bool result = exec.Execute();

                Assert.Equal(true, result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("errorlevel", oldValue);
            }
        }

        [Fact]
        public void ValidateParametersNoCommand()
        {
            Exec exec = PrepareExec("   ");

            bool result = exec.Execute();

            Assert.Equal(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3072");
        }

        /// <summary>
        /// Verify that the EnvironmentVariables parameter exposed publicly
        /// by ToolTask can be used to modify the environment of the cmd.exe spawned.
        /// </summary>
        [Fact]
        public void SetEnvironmentVariableParameter()
        {
            Exec exec = new Exec();
            exec.BuildEngine = new MockEngine();
            exec.Command = "echo [%MYENVVAR%]";
            exec.EnvironmentVariables = new string[] { "myenvvar=myvalue" };
            exec.Execute();

            ((MockEngine)exec.BuildEngine).AssertLogContains("[myvalue]");
        }

        /// <summary>
        /// Execute return output as an Item
        /// Test include ConsoleToMSBuild, StandardOutput
        /// </summary>
        [Fact]
        public void ConsoleToMSBuild()
        {
            //Exec with no output
            Exec exec = PrepareExec("set foo=blah");
            //Test Set and Get of ConsoleToMSBuild
            exec.ConsoleToMSBuild = true;
            Assert.Equal(true, exec.ConsoleToMSBuild);

            bool result = exec.Execute();
            Assert.Equal(true, result);

            //Nothing to run, so the list should be empty
            Assert.Equal(0, exec.ConsoleOutput.Length);


            //first echo prints "Hello stderr" to stderr, second echo prints to stdout
            string testString = "echo Hello stderr 1>&2\necho Hello stdout";
            exec = PrepareExec(testString);

            //Test Set and Get of ConsoleToMSBuild
            exec.ConsoleToMSBuild = true;
            Assert.Equal(true, exec.ConsoleToMSBuild);

            result = exec.Execute();
            Assert.Equal(true, result);

            //Both two lines should had gone to stdout
            Assert.Equal(2, exec.ConsoleOutput.Length);
        }
    }

    internal class ExecWrapper : Exec
    {
        public Encoding StdOutputEncoding
        {
            get
            {
                return StandardOutputEncoding;
            }
        }

        public Encoding StdErrorEncoding
        {
            get
            {
                return StandardErrorEncoding;
            }
        }
    }
}



