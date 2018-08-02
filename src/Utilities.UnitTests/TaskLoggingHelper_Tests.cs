// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class TaskLoggingHelperTests
    {
        [Fact]
        public void CheckMessageCode()
        {
            Task t = new MockTask();

            // normal
            string messageOnly;
            string code = t.Log.ExtractMessageCode("AL001: This is a message.", out messageOnly);
            code.ShouldBe("AL001");
            messageOnly.ShouldBe("This is a message.");

            // whitespace before code and after colon is ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("  AL001:   This is a message.", out messageOnly);
            code.ShouldBe("AL001");
            messageOnly.ShouldBe("This is a message.");

            // whitespace after colon is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("AL001 : This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("AL001 : This is a message.");

            // big code is ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("  RESGEN7905001:   This is a message.", out messageOnly);
            code.ShouldBe("RESGEN7905001");
            messageOnly.ShouldBe("This is a message.");

            // small code is ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("R7: This is a message.", out messageOnly);
            code.ShouldBe("R7");
            messageOnly.ShouldBe("This is a message.");

            // lowercase code is ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("alink3456: This is a message.", out messageOnly);
            code.ShouldBe("alink3456");
            messageOnly.ShouldBe("This is a message.");

            // whitespace in code is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("  RES 7905:   This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("  RES 7905:   This is a message.");

            // only digits in code is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("7905: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("7905: This is a message.");

            // only letters in code is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("ALINK: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("ALINK: This is a message.");

            // digits before letters in code is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("6780ALINK: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("6780ALINK: This is a message.");

            // mixing digits and letters in code is not ok
            messageOnly = null;
            code = t.Log.ExtractMessageCode("LNK658A: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("LNK658A: This is a message.");
        }

        /// <summary>
        /// LogMessageFromStream parses the stream and decides if it is an error/warning/message.
        /// The way it figures out if a message is an error or warning is by parsing it against
        /// the canonical error/warning format.  If it happens to be an error this method returns
        /// true ... isError.  This unit test ensures that passing a canonical error format results
        /// in this method returning true and passing a non canonical message results in it returning
        /// false
        /// </summary>
        [Fact]
        public void CheckMessageFromStreamParsesErrorsAndMessagesCorrectly()
        {
            IBuildEngine2 mockEngine = new MockEngine();
            Task t = new MockTask();
            t.BuildEngine = mockEngine;

            // This should return true since I am passing a canonical error as the stream
            StringReader sr = new StringReader("error MSB4040: There is no target in the project.");
            t.Log.LogMessagesFromStream(sr, MessageImportance.High).ShouldBeTrue();

            // This should return false since I am passing a canonical warning as the stream
            sr = new StringReader("warning ABCD123MyCode: Felix is a cat.");
            t.Log.LogMessagesFromStream(sr, MessageImportance.Low).ShouldBeFalse();

            // This should return false since I am passing a non canonical message in the stream
            sr = new StringReader("Hello World");
            t.Log.LogMessagesFromStream(sr, MessageImportance.High).ShouldBeFalse();
        }

        [Fact]
        public void LogCommandLine()
        {
            MockEngine mockEngine = new MockEngine();
            Task t = new MockTask();
            t.BuildEngine = mockEngine;

            t.Log.LogCommandLine("MySuperCommand");
            mockEngine.Log.ShouldContain("MySuperCommand");
        }

        /// <summary>
        /// This verifies that we don't try to run FormatString on a string
        /// that isn't a resource (if we did, the unmatched curly would give an exception)
        /// </summary>
        [Fact]
        public void LogMessageWithUnmatchedCurly()
        {
            MockEngine mockEngine = new MockEngine();
            Task t = new MockTask();
            t.BuildEngine = mockEngine;

            t.Log.LogMessage("echo {");
            t.Log.LogMessageFromText("{1", MessageImportance.High);
            t.Log.LogCommandLine("{2");
            t.Log.LogWarning("{3");
            t.Log.LogError("{4");

            mockEngine.AssertLogContains("echo {");
            mockEngine.AssertLogContains("{1");
            mockEngine.AssertLogContains("{2");
            mockEngine.AssertLogContains("{3");
            mockEngine.AssertLogContains("{4");
        }

        [Fact]
        public void LogFromResources()
        {
            MockEngine mockEngine = new MockEngine();
            Task t = new MockTask();
            t.BuildEngine = mockEngine;

            t.Log.LogErrorFromResources("MySubcategoryResource", null,
                "helpkeyword", "filename", 1, 2, 3, 4, "MyErrorResource", "foo");

            t.Log.LogErrorFromResources("MyErrorResource", "foo");

            t.Log.LogWarningFromResources("MySubcategoryResource", null,
                "helpkeyword", "filename", 1, 2, 3, 4, "MyWarningResource", "foo");

            t.Log.LogWarningFromResources("MyWarningResource", "foo");

            mockEngine.Log.Contains("filename(1,2,3,4): Romulan error : Oops I wiped your harddrive foo").ShouldBeTrue();
            mockEngine.Log.Contains("filename(1,2,3,4): Romulan warning : Be nice or I wipe your harddrive foo").ShouldBeTrue();
            mockEngine.Log.Contains("Oops I wiped your harddrive foo").ShouldBeTrue();
            mockEngine.Log.Contains("Be nice or I wipe your harddrive foo").ShouldBeTrue();
        }

        [Fact]
        public void CheckLogMessageFromFile()
        {
            string file = null;

            try
            {
                file = FileUtilities.GetTemporaryFile();

                string contents = @"a message here
                    error abcd12345: hey jude.
                    warning xy11: I wanna hold your hand.
                    this is not an error or warning
                    nor is this
                    error def222: norwegian wood";

                // This closes the reader
                File.WriteAllText(file, contents);

                MockEngine mockEngine = new MockEngine();
                Task t = new MockTask();
                t.BuildEngine = mockEngine;
                t.Log.LogMessagesFromFile(file, MessageImportance.High);

                mockEngine.Errors.ShouldBe(2);
                mockEngine.Warnings.ShouldBe(1);
                mockEngine.Messages.ShouldBe(3);

                mockEngine = new MockEngine();
                t = new MockTask();
                t.BuildEngine = mockEngine;
                t.Log.LogMessagesFromFile(file);

                mockEngine.Errors.ShouldBe(2);
                mockEngine.Warnings.ShouldBe(1);
                mockEngine.Messages.ShouldBe(3);
            }
            finally
            {
                if (null != file) File.Delete(file);
            }
        }

        [Fact]
        public void CheckResourcesRegistered()
        {
            Should.Throw<InvalidOperationException>(() =>
            {
                Task t = new MockTask(false /*don't register resources*/);

                try
                {
                    t.Log.FormatResourceString("bogus");
                }
                catch (Exception e)
                {
                    // so I can see the exception message in NUnit's "Standard Out" window
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
           );
        }
        /// <summary>
        /// Verify the LogErrorFromException & LogWarningFromException methods
        /// </summary>
        [Fact]
        public void TestLogFromException()
        {
            string message = "exception message";
            string stackTrace = "TaskLoggingHelperTests.TestLogFromException";

            MockEngine engine = new MockEngine();
            MockTask task = new MockTask();
            task.BuildEngine = engine;

            // need to throw and catch an exception so that its stack trace is initialized to something
            try
            {
                Exception inner = new InvalidOperationException();
                throw new Exception(message, inner);
            }
            catch (Exception e)
            {
                // log error without stack trace
                task.Log.LogErrorFromException(e);
                engine.AssertLogContains(message);
                engine.AssertLogDoesntContain(stackTrace);
                engine.AssertLogDoesntContain("InvalidOperationException");

                engine.Log = string.Empty;

                // log warning with stack trace
                task.Log.LogWarningFromException(e);
                engine.AssertLogContains(message);
                engine.AssertLogDoesntContain(stackTrace);

                engine.Log = string.Empty;

                // log error with stack trace
                task.Log.LogErrorFromException(e, true);
                engine.AssertLogContains(message);
                engine.AssertLogContains(stackTrace);
                engine.AssertLogDoesntContain("InvalidOperationException");

                engine.Log = string.Empty;

                // log warning with stack trace
                task.Log.LogWarningFromException(e, true);
                engine.AssertLogContains(message);
                engine.AssertLogContains(stackTrace);
                engine.Log = string.Empty;

                // log error with stack trace and inner exceptions
                task.Log.LogErrorFromException(e, true, true, "foo.cs");
                engine.AssertLogContains(message);
                engine.AssertLogContains(stackTrace);
                engine.AssertLogContains("InvalidOperationException");
            }
        }
    }
}
