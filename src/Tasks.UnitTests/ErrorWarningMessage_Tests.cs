// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class ErrorWarningMessage_Tests
    {
        /// <summary>
        /// Simple case
        /// </summary>
        [MSBuildTestMethod]
        public void Message()
        {
            MockEngine e = new MockEngine();
            Message m = new Message();
            m.BuildEngine = e;

            m.Text = "messagetext";

            bool retval = m.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.AreNotEqual(-1, e.Log.IndexOf("messagetext", StringComparison.Ordinal));
        }

        /// <summary>
        /// Multiple lines
        /// </summary>
        [MSBuildTestMethod]
        public void MultilineMessage()
        {
            MockEngine e = new MockEngine();
            Message m = new Message();
            m.BuildEngine = e;

            m.Text = "messagetext\n  messagetext2  \n\nmessagetext3";

            bool retval = m.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.AreNotEqual(-1, e.Log.IndexOf("messagetext\n  messagetext2  \n\nmessagetext3", StringComparison.Ordinal));
        }

        /// <summary>
        /// Empty message should not log an event
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyMessage()
        {
            MockEngine e = new MockEngine();
            Message m = new Message();
            m.BuildEngine = e;

            // don't set text

            bool retval = m.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.AreEqual(0, e.Messages);
        }

        /// <summary>
        /// Simple case
        /// </summary>
        [MSBuildTestMethod]
        public void Warning()
        {
            MockEngine e = new MockEngine(true);
            Warning w = new Warning();
            w.BuildEngine = e;

            w.Text = "warningtext";
            w.File = "c:\\file";

            bool retval = w.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            e.AssertLogContains("c:\\file(0,0): WARNING : warningtext");
            Assert.AreEqual(1, e.Warnings);
        }

        /// <summary>
        /// Empty warning SHOULD log an event
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyWarning()
        {
            MockEngine e = new MockEngine();
            Warning w = new Warning
            {
                BuildEngine = e
                // don't set text
            };

            bool retval = w.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.AreEqual(1, e.Warnings);
            Assert.Contains(AssemblyResources.GetString("ErrorAndWarning.EmptyMessage"), e.Log);
        }

        /// <summary>
        /// Empty warning message but a code specified should still be logged
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyWarningMessageButCodeSpecified()
        {
            MockEngine e = new MockEngine();
            Warning w = new Warning
            {
                BuildEngine = e,
                Code = "123"
                // don't set text
            };

            bool retval = w.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.AreEqual(1, e.Warnings);
            Assert.Contains(AssemblyResources.GetString("ErrorAndWarning.EmptyMessage"), e.Log);
        }

        /// <summary>
        /// Empty error SHOULD log an event
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyError()
        {
            MockEngine e = new MockEngine();
            Error err = new Error
            {
                BuildEngine = e
                // don't set text
            };



            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);
            Assert.AreEqual(1, e.Errors);
            Assert.Contains(AssemblyResources.GetString("ErrorAndWarning.EmptyMessage"), e.Log);
        }

        /// <summary>
        /// Empty error message but a code specified should still be logged
        /// </summary>
        [MSBuildTestMethod]
        public void EmptyErrorMessageButCodeSpecified()
        {
            MockEngine e = new MockEngine();
            Error err = new Error
            {
                BuildEngine = e,
                Code = "999"
                // don't set text
            };


            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);
            Assert.AreEqual(1, e.Errors);
            Assert.Contains(AssemblyResources.GetString("ErrorAndWarning.EmptyMessage"), e.Log);
        }

        /// <summary>
        /// Simple case
        /// </summary>
        [MSBuildTestMethod]
        public void Error()
        {
            MockEngine e = new MockEngine(true);
            Error err = new Error();
            err.BuildEngine = e;

            err.Text = "errortext";
            err.File = "c:\\file";

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);
            e.AssertLogContains("c:\\file(0,0): ERROR : errortext");
            Assert.AreEqual(1, e.Errors);
        }

        /// <summary>
        /// Simple case for error message coming from a resource string
        /// </summary>
        [MSBuildTestMethod]
        public void ErrorFromResources()
        {
            MockEngine e = new MockEngine(true);
            ErrorFromResources err = new ErrorFromResources();
            err.BuildEngine = e;

            err.Resource = "Exec.MissingCommandError";

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);

            string message = AssemblyResources.GetString(err.Resource);
            e.AssertLogContains(message);
            Assert.AreEqual(1, e.Errors);
        }

        /// <summary>
        /// If a "Code" is passed to the task, use it to override the code
        /// (if any) defined in the error message.
        /// </summary>
        [MSBuildTestMethod]
        public void ErrorFromResourcesWithOverriddenCode()
        {
            MockEngine e = new MockEngine(true);
            ErrorFromResources err = new ErrorFromResources();
            err.BuildEngine = e;

            err.Resource = "Exec.MissingCommandError";
            err.Code = "ABC1234";

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);

            string message = AssemblyResources.GetString(err.Resource);
            string updatedMessage = message.Replace("MSB3072", "ABC1234");
            e.AssertLogContains(updatedMessage);
            Assert.AreEqual(1, e.Errors);
        }

        /// <summary>
        /// Simple case of logging a resource-based error that takes
        /// arguments
        /// </summary>
        [MSBuildTestMethod]
        public void ErrorFromResourcesWithArguments()
        {
            MockEngine e = new MockEngine(true);
            ErrorFromResources err = new ErrorFromResources();
            err.BuildEngine = e;

            err.Resource = "Copy.Error";
            err.Arguments = new string[] { "a.txt", "b.txt", "xyz" };

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);

            string message = String.Format(AssemblyResources.GetString(err.Resource), err.Arguments);
            e.AssertLogContains(message);
            Assert.AreEqual(1, e.Errors);
        }

        /// <summary>
        /// If invalid arguments are passed to the task, it should still
        /// log an error informing the user of that.
        /// </summary>
        [MSBuildTestMethod]
        public void ErrorFromResourcesWithInvalidArguments()
        {
            MockEngine e = new MockEngine(true);
            ErrorFromResources err = new ErrorFromResources();
            err.BuildEngine = e;

            err.Resource = "Copy.Error";
            err.Arguments = new string[] { "a.txt", "b.txt" };

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsFalse(retval);

            e.AssertLogDoesntContain("a.txt");
            e.AssertLogContains("MSB3861");
            Assert.AreEqual(1, e.Errors);
        }

        /// <summary>
        /// If no resource string is passed to ErrorFromResources, we should error
        /// because a required parameter is missing.
        /// </summary>
        [MSBuildTestMethod]
        public void ErrorFromResourcesNoResources()
        {
            string projectContents = @"
<Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
  <Target Name=`Build`>
    <ErrorFromResources />
  </Target>
</Project>
";

            MockLogger logger = ObjectModelHelpers.BuildProjectExpectFailure(projectContents);

            // missing required parameter
            logger.AssertLogContains("MSB4044");
        }
    }
}
