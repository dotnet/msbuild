// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the Exec task
    /// </summary>
    [TestClass]
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
        [TestMethod]
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
            Assert.IsTrue(result);
            // Ensure the new temp file count equals the old temp file count.
            Assert.AreEqual(originalTempFileCount, newTempFileCount);
        }

        [TestMethod]
        public void ExitCodeCausesFailure()
        {
            Exec exec = PrepareExec("xcopy thisisanonexistentfile");
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(4, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3073");
        }

        [TestMethod]
        [Ignore] // "Timing issue found on RI candidate from ToolPlat to Main, disabling for RI only."
        public void Timeout()
        {
            Exec exec = PrepareExec(":foo \n goto foo");
            exec.Timeout = 5;
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(-1, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB5002");
            Assert.AreEqual(1, ((MockEngine)exec.BuildEngine).Warnings);
            Assert.AreEqual(1, ((MockEngine)exec.BuildEngine).Errors);
        }

        [TestMethod]
        public void ExitCodeGetter()
        {
            Exec exec = PrepareExec("exit 666");
            bool result = exec.Execute();

            Assert.AreEqual(666, exec.ExitCode);
        }

        [TestMethod]
        public void LoggedErrorsCauseFailureDespiteExitCode0()
        {
            // This will return 0 exit code, but emitted a canonical error
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror");
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            // Exitcode is set to -1
            Assert.AreEqual(-1, exec.ExitCode);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3073");
        }

        [TestMethod]
        public void IgnoreExitCodeTrueMakesTaskSucceedDespiteLoggingErrors()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror");
            exec.IgnoreExitCode = true;
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void IgnoreExitCodeTrueMakesTaskSucceedDespiteExitCode1()
        {
            Exec exec = PrepareExec("dir ||invalid||");
            exec.IgnoreExitCode = true;
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
        }

        [TestMethod]
        public void NonUNCWorkingDirectoryUsed()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            string working = Environment.GetFolderPath(Environment.SpecialFolder.Windows); // not desktop etc - IT redirection messes it up
            exec.WorkingDirectory = working;
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("[" + working + "]");
        }

        [TestMethod]
        public void UNCWorkingDirectoryUsed()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            string working = @"\\" + Environment.MachineName + @"\c$";
            exec.WorkingDirectory = working;
            bool result = exec.ValidateParametersAccessor();

            Assert.AreEqual(true, result);
            Assert.AreEqual(true, exec.workingDirectoryIsUNC);
            Assert.AreEqual(working, exec.WorkingDirectory);
            // Should give ToolTask the system folder as the working directory, when it's a UNC
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            Assert.AreEqual(system, exec.GetWorkingDirectoryAccessor());
        }

        [TestMethod]
        public void NoWorkingDirectorySet()
        {
            var cd = Directory.GetCurrentDirectory();

            try
            {
                Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

                Exec exec = PrepareExec("echo [%cd%]");
                bool result = exec.Execute();

                string expected = Directory.GetCurrentDirectory();
                Assert.AreEqual(true, result);
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
        [TestMethod]
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

                Assert.IsTrue(exec.Execute(), "Task should have succeeded");
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
        [TestMethod]
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
                Assert.IsTrue(taskSucceeded, "Task should have succeeded");
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
        [TestMethod]
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

                Assert.IsTrue(exec.Execute(), "Task should have succeeded");
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
        [TestMethod]
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

                Assert.IsTrue(exec.Execute(), "Task should have succeeded");
                ((MockEngine)exec.BuildEngine).AssertLogContains("[hello]");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                if (Directory.Exists(newTmp)) Directory.Delete(newTmp);
            }
        }

        [TestMethod]
        public void InvalidUncDirectorySet()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = @"\\thiscomputerdoesnotexistxyz\thiscomputerdoesnotexistxyz";
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB6003");
        }

        [TestMethod]
        public void InvalidWorkingDirectorySet()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = @"||invalid||";
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB6003");
        }

        [TestMethod]
        public void BogusCustomRegexesCauseOneErrorEach()
        {
            Exec exec = PrepareExec("echo Some output & echo Some output & echo Some output & echo Some output ");
            exec.CustomErrorRegularExpression = "~!@#$%^_)(*&^%$#@@#XF &%^%T$REd((((([[[[";
            exec.CustomWarningRegularExpression = "*";
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.AreEqual(3, e.Errors);
            e.AssertLogContains("MSB3076");
        }

        [TestMethod]
        public void CustomErrorRegexSupplied()
        {
            Exec exec = PrepareExec("echo Some output & echo ALERT:This is an error & echo Some more output");
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.AreEqual(0, e.Errors);
            e.AssertLogContains("ALERT:This is an error");

            exec = PrepareExec("echo Some output & echo ALERT:This is an error & echo Some more output");
            exec.CustomErrorRegularExpression = ".*ALERT.*";
            result = exec.Execute();

            e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.AreEqual(2, e.Errors);
            e.AssertLogContains("ALERT:This is an error");
        }

        [TestMethod]
        public void CustomWarningRegexSupplied()
        {
            Exec exec = PrepareExec("echo Some output & echo YOOHOO:This is a warning & echo Some more output");
            bool result = exec.Execute();

            MockEngine e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.AreEqual(0, e.Errors);
            Assert.AreEqual(0, e.Warnings);
            e.AssertLogContains("YOOHOO:This is a warning");

            exec = PrepareExec("echo Some output & echo YOOHOO:This is a warning & echo Some more output");
            exec.CustomWarningRegularExpression = ".*YOOHOO.*";
            result = exec.Execute();

            e = (MockEngine)exec.BuildEngine;
            Console.WriteLine(e.Log);
            Assert.AreEqual(0, e.Errors);
            Assert.AreEqual(1, e.Warnings);
            e.AssertLogContains("YOOHOO:This is a warning");
        }

        [TestMethod]
        public void ErrorsAndWarningsWithIgnoreStandardErrorWarningFormatTrue()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning");
            exec.IgnoreStandardErrorWarningFormat = true;
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [TestMethod]
        public void CustomAndStandardErrorsAndWarnings()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning & echo YOGI & echo BEAR & echo some content");
            exec.CustomWarningRegularExpression = ".*BEAR.*";
            exec.CustomErrorRegularExpression = ".*YOGI.*";
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(3, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(2, ((MockEngine)exec.BuildEngine).Warnings);
        }

        /// <summary>
        /// Nobody should try to run a string emitted from the task through String.Format.
        /// Firstly that's unnecessary and secondly if there's eg an unmatched curly it will throw.
        /// </summary>
        [TestMethod]
        public void DoNotAttemptToFormatTaskOutput()
        {
            Exec exec = PrepareExec("echo unmatched curly {");
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("unmatched curly {");
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        /// <summary>
        /// Nobody should try to run a string emitted from the task through String.Format.
        /// Firstly that's unnecessary and secondly if there's eg an unmatched curly it will throw.
        /// </summary>
        [TestMethod]
        public void DoNotAttemptToFormatTaskOutput2()
        {
            Exec exec = PrepareExec("echo unmatched curly {");
            exec.IgnoreStandardErrorWarningFormat = true;
            bool result = exec.Execute();

            Assert.AreEqual(true, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("unmatched curly {");
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [TestMethod]
        public void NoDuplicateMessagesWhenCustomRegexAndRegularRegexBothMatch()
        {
            Exec exec = PrepareExec("echo myfile(88,37): error AB1234: thisisacanonicalerror & echo foo: warning CDE1234: thisisacanonicalwarning ");
            exec.CustomErrorRegularExpression = ".*canonicale.*";
            exec.CustomWarningRegularExpression = ".*canonicalw.*";
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(2, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(1, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [TestMethod]
        public void OnlySingleErrorWhenCustomWarningAndCustomErrorRegexesBothMatch()
        {
            Exec exec = PrepareExec("echo YOGI BEAR ");
            exec.CustomErrorRegularExpression = ".*YOGI.*";
            exec.CustomWarningRegularExpression = ".*BEAR.*";
            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            Assert.AreEqual(2, ((MockEngine)exec.BuildEngine).Errors);
            Assert.AreEqual(0, ((MockEngine)exec.BuildEngine).Warnings);
        }

        [TestMethod]
        public void GettersSetters()
        {
            Exec exec = PrepareExec("echo [%cd%]");
            exec.WorkingDirectory = "foo";
            Assert.AreEqual("foo", exec.WorkingDirectory);
            exec.IgnoreExitCode = true;
            Assert.AreEqual(true, exec.IgnoreExitCode);
            exec.Outputs = null;
            Assert.AreEqual(0, exec.Outputs.Length);

            ITaskItem[] items = new TaskItem[] { new TaskItem("hi"), new TaskItem("ho") };
            exec.Outputs = items;
            Assert.AreEqual(items, exec.Outputs);
        }

        [TestMethod]
        public void StdEncodings()
        {
            ExecWrapper exec = PrepareExecWrapper("echo [%cd%]");

            exec.StdErrEncoding = "US-ASCII";
            Assert.AreEqual(true, exec.StdErrEncoding.Contains("US-ASCII"));
            Assert.AreEqual(true, exec.StdErrorEncoding.EncodingName.Contains("US-ASCII"));

            exec.StdOutEncoding = "US-ASCII";
            Assert.AreEqual(true, exec.StdOutEncoding.Contains("US-ASCII"));
            Assert.AreEqual(true, exec.StdOutputEncoding.EncodingName.Contains("US-ASCII"));
        }

        [TestMethod]
        public void AnyExistingEnvVarCalledErrorLevelIsIgnored()
        {
            string oldValue = Environment.GetEnvironmentVariable("errorlevel");

            try
            {
                Exec exec = PrepareExec("echo this is an innocuous successful command");
                Environment.SetEnvironmentVariable("errorlevel", "1");
                bool result = exec.Execute();

                Assert.AreEqual(true, result);
            }
            finally
            {
                Environment.SetEnvironmentVariable("errorlevel", oldValue);
            }
        }

        [TestMethod]
        public void ValidateParametersNoCommand()
        {
            Exec exec = PrepareExec("   ");

            bool result = exec.Execute();

            Assert.AreEqual(false, result);
            ((MockEngine)exec.BuildEngine).AssertLogContains("MSB3072");
        }

        /// <summary>
        /// Verify that the EnvironmentVariables parameter exposed publicly
        /// by ToolTask can be used to modify the environment of the cmd.exe spawned.
        /// </summary>
        [TestMethod]
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
        [TestMethod]
        public void ConsoleToMSBuild()
        {
            //Exec with no output
            Exec exec = PrepareExec("set foo=blah");
            //Test Set and Get of ConsoleToMSBuild
            exec.ConsoleToMSBuild = true;
            Assert.AreEqual(true, exec.ConsoleToMSBuild);

            bool result = exec.Execute();
            Assert.AreEqual(true, result);

            //Nothing to run, so the list should be empty
            Assert.AreEqual(0, exec.ConsoleOutput.Length);


            //first echo prints "Hello stderr" to stderr, second echo prints to stdout
            string testString = "echo Hello stderr 1>&2\necho Hello stdout";
            exec = PrepareExec(testString);

            //Test Set and Get of ConsoleToMSBuild
            exec.ConsoleToMSBuild = true;
            Assert.AreEqual(true, exec.ConsoleToMSBuild);

            result = exec.Execute();
            Assert.AreEqual(true, result);

            //Both two lines should had gone to stdout
            Assert.AreEqual(2, exec.ConsoleOutput.Length);
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



