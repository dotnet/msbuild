// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class TaskLoggingHelperTests
    {
#if NET
        private readonly ITestOutputHelper _output;

        public TaskLoggingHelperTests(ITestOutputHelper output) => _output = output;
#endif

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
            code = t.Log.ExtractMessageCode("  AL001:   This is a message.", out messageOnly);
            code.ShouldBe("AL001");
            messageOnly.ShouldBe("This is a message.");

            // whitespace after colon is not ok
            code = t.Log.ExtractMessageCode("AL001 : This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("AL001 : This is a message.");

            // big code is ok
            code = t.Log.ExtractMessageCode("  RESGEN7905001:   This is a message.", out messageOnly);
            code.ShouldBe("RESGEN7905001");
            messageOnly.ShouldBe("This is a message.");

            // small code is ok
            code = t.Log.ExtractMessageCode("R7: This is a message.", out messageOnly);
            code.ShouldBe("R7");
            messageOnly.ShouldBe("This is a message.");

            // lowercase code is ok
            code = t.Log.ExtractMessageCode("alink3456: This is a message.", out messageOnly);
            code.ShouldBe("alink3456");
            messageOnly.ShouldBe("This is a message.");

            // whitespace in code is not ok
            code = t.Log.ExtractMessageCode("  RES 7905:   This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("  RES 7905:   This is a message.");

            // only digits in code is not ok
            code = t.Log.ExtractMessageCode("7905: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("7905: This is a message.");

            // only letters in code is not ok
            code = t.Log.ExtractMessageCode("ALINK: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("ALINK: This is a message.");

            // digits before letters in code is not ok
            code = t.Log.ExtractMessageCode("6780ALINK: This is a message.", out messageOnly);
            code.ShouldBeNull();
            messageOnly.ShouldBe("6780ALINK: This is a message.");

            // mixing digits and letters in code is not ok
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
            IBuildEngine2 mockEngine = new MockEngine3();
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
            MockEngine3 mockEngine = new MockEngine3();
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
            MockEngine3 mockEngine = new MockEngine3();
            Task t = new MockTask();
            t.BuildEngine = mockEngine;

#pragma warning disable CA2241 // Format argument invalid. True! But exactly what we're testing here.
            t.Log.LogMessage("echo {");
            t.Log.LogMessageFromText("{1", MessageImportance.High);
            t.Log.LogCommandLine("{2");
            t.Log.LogWarning("{3");
            t.Log.LogError("{4");
#pragma warning restore CA2241

            mockEngine.AssertLogContains("echo {");
            mockEngine.AssertLogContains("{1");
            mockEngine.AssertLogContains("{2");
            mockEngine.AssertLogContains("{3");
            mockEngine.AssertLogContains("{4");
        }

        [Fact]
        public void InterpolatedMessageUsesStructuredOverload()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };
            string candidate = "a.dll";
            string expected = "b.dll";

            task.Log.LogMessage(MessageImportance.Low, $"Considered {candidate} but expected {expected}");

            mockEngine.LastMessageEvent.Message.ShouldBe("Considered a.dll but expected b.dll");
            IStructuredBuildEventArgs structured = mockEngine.LastMessageEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>();
            structured.OriginalFormat.ShouldBe("Considered {candidate} but expected {expected}");
            structured.StructuredValues.ShouldBe(
            [
                new KeyValuePair<string, string>("candidate", "a.dll"),
                new KeyValuePair<string, string>("expected", "b.dll"),
            ]);
        }

        [Fact]
        public void StructuredMessageRemainsLazyAndNamedStateSurvivesMaterialization()
        {
            MockEngine3 mockEngine = new() { MinimumMessageImportance = MessageImportance.High };
            Task task = new MockTask { BuildEngine = mockEngine };
            string value = "captured";

            task.Log.LogMessage($"Value {value}");

            StructuredBuildMessageEventArgs buildEvent =
                mockEngine.LastMessageEvent.ShouldBeOfType<StructuredBuildMessageEventArgs>();
            buildEvent.RawMessage.ShouldBe("Value {value}");
            buildEvent.RawArguments.ShouldBeNull();
            buildEvent.OriginalFormat.ShouldBe("Value {value}");
            buildEvent.StructuredValues[0].Value.ShouldBe("captured");

            buildEvent.Message.ShouldBe("Value captured");
            buildEvent.OriginalFormat.ShouldBe("Value {value}");
            buildEvent.StructuredValues[0].Value.ShouldBe("captured");
        }

#if NET
        [Fact]
        public void StructuredMessageUsesOriginalEventShapeWhenChangeWaveIsDisabled()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable(
                "MSBUILDDISABLEFEATURESFROMVERSION",
                ChangeWaves.Wave18_11.ToString());

            try
            {
                MockEngine3 mockEngine = new();
                Task task = new MockTask { BuildEngine = mockEngine };
                string value = "captured";

                task.Log.LogMessage($"Value {value}");

                mockEngine.LastMessageEvent.ShouldBeOfType<BuildMessageEventArgs>();
                mockEngine.LastMessageEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();
                mockEngine.LastMessageEvent.Message.ShouldBe("Value captured");

                task.Log.LogWarning($"Warning {value}");
                mockEngine.LastWarningEvent.ShouldBeOfType<BuildWarningEventArgs>();
                mockEngine.LastWarningEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();
                mockEngine.LastWarningEvent.Message.ShouldBe("Warning captured");

                task.Log.LogError($"Error {value}");
                mockEngine.LastErrorEvent.ShouldBeOfType<BuildErrorEventArgs>();
                mockEngine.LastErrorEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();
                mockEngine.LastErrorEvent.Message.ShouldBe("Error captured");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }
#endif

        [Fact]
        public void StringAndCompositeMessageOverloadsRemainUnstructured()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };

            task.Log.LogMessage("literal");
            mockEngine.LastMessageEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();

            task.Log.LogMessage("composite {0}", 42);
            mockEngine.LastMessageEvent.Message.ShouldBe("composite 42");
            mockEngine.LastMessageEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();

            string preformatted = $"preformatted {42}";
            task.Log.LogMessage(preformatted);
            mockEngine.LastMessageEvent.ShouldNotBeAssignableTo<IStructuredBuildEventArgs>();
        }

        [Fact]
        public void StructuredInterpolationSupportsNamesFormattingNullsAndBraces()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };
            int amount = 12;
            string missing = null;
            string projectPath = "project.proj";

            task.Log.LogMessage(
                $"{{value}} {amount,8:D4} {amount:D2} {missing} {task.Log.Named("ProjectPath", projectPath)}");

            mockEngine.LastMessageEvent.Message.ShouldBe("{value}     0012 12  project.proj");
            IStructuredBuildEventArgs structured = mockEngine.LastMessageEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>();
            structured.OriginalFormat.ShouldBe(
                "{{value}} {amount,8:D4} {amount_2:D2} {missing} {ProjectPath}");
            structured.StructuredValues.ShouldBe(
            [
                new KeyValuePair<string, string>("amount", "0012"),
                new KeyValuePair<string, string>("amount_2", "12"),
                new KeyValuePair<string, string>("missing", null),
                new KeyValuePair<string, string>("ProjectPath", "project.proj"),
            ]);
        }

        [Fact]
        public void DisabledImportanceDoesNotEvaluateInterpolationHoles()
        {
            MockEngine mockEngine = new() { MinimumMessageImportance = MessageImportance.High };
            Task task = new MockTask { BuildEngine = mockEngine };
            int evaluations = 0;

            task.Log.LogMessage(MessageImportance.Low, $"Not evaluated {Evaluate()}");

            evaluations.ShouldBe(0);
            mockEngine.Messages.ShouldBe(0);

            int Evaluate()
            {
                evaluations++;
                return evaluations;
            }
        }

        [Fact]
        public void ManuallyDisabledHandlerIsIgnoredByWarningAndErrorOverloads()
        {
            MockEngine mockEngine = new() { MinimumMessageImportance = MessageImportance.High };
            Task task = new MockTask { BuildEngine = mockEngine };
            var handler = new TaskLoggingHelper.StructuredLogInterpolatedStringHandler(
                literalLength: 0,
                formattedCount: 0,
                task.Log,
                MessageImportance.Low,
                out bool shouldAppend);

            shouldAppend.ShouldBeFalse();
            task.Log.LogWarning(ref handler);
            task.Log.LogError(ref handler);

            mockEngine.Warnings.ShouldBe(0);
            mockEngine.Errors.ShouldBe(0);
        }

#if NET
        [Fact]
        public void DisabledStructuredInterpolationAllocatesLessThanEagerFormatting()
        {
            MockEngine mockEngine = new() { MinimumMessageImportance = MessageImportance.High };
            Task task = new MockTask { BuildEngine = mockEngine };

            for (int i = 0; i < 10; i++)
            {
                task.Log.LogMessage(MessageImportance.Low, $"Value {i}");
                string eager = $"Value {i}";
                task.Log.LogMessage(MessageImportance.Low, eager);
            }

            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000; i++)
            {
                task.Log.LogMessage(MessageImportance.Low, $"Value {i}");
            }

            long structuredAllocations = GC.GetAllocatedBytesForCurrentThread() - start;

            start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000; i++)
            {
                string eager = $"Value {i}";
                task.Log.LogMessage(MessageImportance.Low, eager);
            }

            long eagerAllocations = GC.GetAllocatedBytesForCurrentThread() - start;

            _output.WriteLine(
                $"Disabled structured interpolation: {structuredAllocations:N0} bytes; eager interpolation: {eagerAllocations:N0} bytes.");
            structuredAllocations.ShouldBeLessThan(eagerAllocations);
        }
#endif

        [Fact]
        public void ExplicitStructuredApisSupportOrderedValuesAndLocalizedDisplay()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };

            task.Log.LogStructuredMessage("Copied {Source} to {Destination}", "a", "b");
            mockEngine.LastMessageEvent.Message.ShouldBe("Copied a to b");
            IStructuredBuildEventArgs structured = mockEngine.LastMessageEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>();
            structured.OriginalFormat.ShouldBe("Copied {Source} to {Destination}");

            task.Log.LogStructuredMessage(
                MessageImportance.Normal,
                "Copied {Source} to {Destination}",
                "b <- a",
                new List<KeyValuePair<string, object>>
                {
                    new("Source", "a"),
                    new("Destination", "b"),
                });

            mockEngine.LastMessageEvent.Message.ShouldBe("b <- a");
            structured = mockEngine.LastMessageEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>();
            structured.StructuredValues[0].Key.ShouldBe("Source");
            structured.StructuredValues[1].Key.ShouldBe("Destination");
        }

        [Fact]
        public void DynamicStructuredTemplatesValidateAndDisambiguateNames()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };

            task.Log.LogStructuredMessage("{Value} then {Value}", 1, 2);
            IStructuredBuildEventArgs structured = mockEngine.LastMessageEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>();
            structured.OriginalFormat.ShouldBe("{Value} then {Value_2}");
            structured.StructuredValues[0].Key.ShouldBe("Value");
            structured.StructuredValues[1].Key.ShouldBe("Value_2");

            Should.Throw<FormatException>(() => task.Log.LogStructuredMessage("{Missing", 1));
            Should.Throw<FormatException>(() => task.Log.LogStructuredMessage("{One} {Two}", 1));
            Should.Throw<ArgumentException>(() => task.Log.LogStructuredMessage(
                MessageImportance.Normal,
                "{Expected}",
                "localized",
                new List<KeyValuePair<string, object>> { new("Actual", 1) }));
            Should.Throw<ArgumentNullException>(() => task.Log.LogStructuredWarning(
                "{Expected}",
                null!,
                new List<KeyValuePair<string, object>> { new("Expected", 1) }));
            Should.Throw<ArgumentNullException>(() => task.Log.LogStructuredError(
                "{Expected}",
                null!,
                new List<KeyValuePair<string, object>> { new("Expected", 1) }));
        }

        [Fact]
        public void StructuredWarningsAndErrorsPreserveDiagnosticMetadata()
        {
            MockEngine3 mockEngine = new();
            Task task = new MockTask { BuildEngine = mockEngine };
            string detail = "detail";

            task.Log.LogWarning("sub", "W1", "help", "helpLink", "file", 1, 2, 3, 4, $"warning {detail}");
            mockEngine.LastWarningEvent.Code.ShouldBe("W1");
            mockEngine.LastWarningEvent.File.ShouldBe("file");
            mockEngine.LastWarningEvent.LineNumber.ShouldBe(1);
            mockEngine.LastWarningEvent.Message.ShouldBe("warning detail");
            mockEngine.LastWarningEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>()
                .OriginalFormat.ShouldBe("warning {detail}");

            task.Log.LogError("sub", "E1", "help", "helpLink", "file", 5, 6, 7, 8, $"error {detail}");
            mockEngine.LastErrorEvent.Code.ShouldBe("E1");
            mockEngine.LastErrorEvent.File.ShouldBe("file");
            mockEngine.LastErrorEvent.LineNumber.ShouldBe(5);
            mockEngine.LastErrorEvent.Message.ShouldBe("error detail");
            mockEngine.LastErrorEvent.ShouldBeAssignableTo<IStructuredBuildEventArgs>()
                .OriginalFormat.ShouldBe("error {detail}");
            task.Log.HasLoggedErrors.ShouldBeTrue();
        }

        [Fact]
        public void StructuredWarningPreservesWarnAsErrorRouting()
        {
            MockEngine mockEngine = new() { TreatWarningsAsErrors = true };
            Task task = new MockTask { BuildEngine = mockEngine };
            string detail = "detail";

            task.Log.LogWarning("sub", "W1", "help", "file", 1, 2, 3, 4, $"warning {detail}");

            mockEngine.Warnings.ShouldBe(0);
            mockEngine.Errors.ShouldBe(1);
            BuildErrorEventArgs error = mockEngine.ErrorEvents.ShouldHaveSingleItem();
            error.Code.ShouldBe("W1");
            error.ShouldBeAssignableTo<IStructuredBuildEventArgs>().OriginalFormat.ShouldBe("warning {detail}");
        }

        [Fact]
        public void LogFromResources()
        {
            MockEngine3 mockEngine = new MockEngine3();
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
                file = FileUtilities.GetTemporaryFileName();

                string contents = @"a message here
                    error abcd12345: hey jude.
                    warning xy11: I wanna hold your hand.
                    this is not an error or warning
                    nor is this
                    error def222: norwegian wood";

                // This closes the reader
                File.WriteAllText(file, contents);

                MockEngine3 mockEngine = new MockEngine3();
                Task t = new MockTask();
                t.BuildEngine = mockEngine;
                t.Log.LogMessagesFromFile(file, MessageImportance.High);

                mockEngine.Errors.ShouldBe(2);
                mockEngine.Warnings.ShouldBe(1);
                mockEngine.Messages.ShouldBe(3);

                mockEngine = new MockEngine3();
                t = new MockTask();
                t.BuildEngine = mockEngine;
                t.Log.LogMessagesFromFile(file);

                mockEngine.Errors.ShouldBe(2);
                mockEngine.Warnings.ShouldBe(1);
                mockEngine.Messages.ShouldBe(3);
            }
            finally
            {
                if (file != null)
                {
                    File.Delete(file);
                }
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
            });
        }
        /// <summary>
        /// Verify the LogErrorFromException & LogWarningFromException methods
        /// </summary>
        [Fact]
        public void TestLogFromException()
        {
            string message = "exception message";
            string stackTrace = "TaskLoggingHelperTests.TestLogFromException";

            MockEngine3 engine = new MockEngine3();
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

        /// <summary>
        /// Verify that <see cref="TaskLoggingHelper.LogErrorFromException(Exception, bool, bool, string)" /> logs inner exceptions from an <see cref="AggregateException" />.
        /// </summary>
        [Fact]
        public void TestLogFromExceptionWithAggregateException()
        {
            AggregateException aggregateException = new AggregateException(
                new InvalidOperationException("The operation was invalid"),
                new IOException("An I/O error occurred"));

            MockEngine3 engine = new MockEngine3();
            MockTask task = new MockTask
            {
                BuildEngine = engine
            };

            task.Log.LogErrorFromException(aggregateException);

            engine.Errors.ShouldBe(2);

            engine.AssertLogContains("The operation was invalid");
            engine.AssertLogContains("An I/O error occurred");
        }
    }
}
