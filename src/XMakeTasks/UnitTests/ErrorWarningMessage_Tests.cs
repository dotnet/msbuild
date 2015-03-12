// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Exceptions;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class ErrorWarningMessage_Tests
    {
        /// <summary>
        /// Simple case
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Log.IndexOf("messagetext") != -1);
        }

        /// <summary>
        /// Multiple lines
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Log.IndexOf("messagetext\n  messagetext2  \n\nmessagetext3") != -1);
        }

        /// <summary>
        /// Empty message should not log an event
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Messages == 0);
        }

        /// <summary>
        /// Simple case
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Warnings == 1);
        }

        /// <summary>
        /// Empty warning should not log an event
        /// </summary>
        [TestMethod]
        public void EmptyWarning()
        {
            MockEngine e = new MockEngine();
            Warning w = new Warning();
            w.BuildEngine = e;

            // don't set text

            bool retval = w.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.IsTrue(e.Warnings == 0);
        }

        /// <summary>
        /// Empty warning message but a code specified should still be logged
        /// </summary>
        [TestMethod]
        public void EmptyWarningMessageButCodeSpecified()
        {
            MockEngine e = new MockEngine();
            Warning w = new Warning();
            w.BuildEngine = e;

            // don't set text
            w.Code = "123";

            bool retval = w.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(retval);
            Assert.IsTrue(e.Warnings == 1);
        }

        /// <summary>
        /// Empty error should not log an event
        /// </summary>
        [TestMethod]
        public void EmptyError()
        {
            MockEngine e = new MockEngine();
            Error err = new Error();
            err.BuildEngine = e;

            // don't set text

            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(false == retval);
            Assert.IsTrue(e.Errors == 0);
        }

        /// <summary>
        /// Empty error message but a code specified should still be logged
        /// </summary>
        [TestMethod]
        public void EmptyErrorMessageButCodeSpecified()
        {
            MockEngine e = new MockEngine();
            Error err = new Error();
            err.BuildEngine = e;

            // don't set text
            err.Code = "999";
            bool retval = err.Execute();

            Console.WriteLine("===");
            Console.WriteLine(e.Log);
            Console.WriteLine("===");

            Assert.IsTrue(false == retval);
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// Simple case
        /// </summary>
        [TestMethod]
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

            Assert.IsTrue(false == retval);
            e.AssertLogContains("c:\\file(0,0): ERROR : errortext");
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// Simple case for error message coming from a resource string
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// If a "Code" is passed to the task, use it to override the code 
        /// (if any) defined in the error message. 
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// Simple case of logging a resource-based error that takes 
        /// arguments
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// If invalid arguments are passed to the task, it should still 
        /// log an error informing the user of that. 
        /// </summary>
        [TestMethod]
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
            Assert.IsTrue(e.Errors == 1);
        }

        /// <summary>
        /// If no resource string is passed to ErrorFromResources, we should error 
        /// because a required parameter is missing. 
        /// </summary>
        [TestMethod]
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



