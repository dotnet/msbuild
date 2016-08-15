// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// These tests are repeated in the Utilities unit test assembly. We know that this isn't
    /// too useful, because both Engine and Utilities pull the code from the same Shared file. But it
    /// gets a bunch of lines of extra coverage of Engine that we weren't otherwise getting, and 
    /// in theory at least the implementation in Engine should be tested too.
    /// </summary>
    [TestFixture]
    public class EventArgsFormattingTests
    {
        [Test]
        public void NoLineInfoFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 0, 0, 0);
            Assertion.AssertEquals(
                      "source.cs : CS error 312: Missing ;", s);
        }

        // Valid forms for line/col number patterns:
        // (line) or (line-line) or (line,col) or (line,col-col) or (line,col,line,col)
        [Test]
        public void LineNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 0, 0);
            Assertion.AssertEquals(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [Test]
        public void ColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 1, 2, 0);
            Assertion.AssertEquals(
                      "source.cs : CS error 312: Missing ;", s);
        }

        [Test]
        public void LineAndColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 4, 0);
            Assertion.AssertEquals(
                      "source.cs(1,3,2,4): CS error 312: Missing ;", s);
        }

        [Test]
        public void LineAndColumnNumberRange2()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 3, 4, 0);
            Assertion.AssertEquals(
                      "source.cs(1,3-4): CS error 312: Missing ;", s);
        }

        [Test]
        public void LineAndColumnNumberRange3()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 0, 0);
            Assertion.AssertEquals(
                      "source.cs(1-2,3): CS error 312: Missing ;", s);
        }

        [Test]
        public void LineAndColumnNumberRange4()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 3, 0);
            Assertion.AssertEquals(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [Test]
        public void LineAndColumnNumberRange5()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 2, 0, 0);
            Assertion.AssertEquals(
                      "source.cs(1,2): CS error 312: Missing ;", s);
        }

        [Test]
        public void BasicFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 42, 0, 0, 0, 0);
            Assertion.AssertEquals(
                      "source.cs(42): CS error 312: Missing ;", s);
        }

        [Test]
        public void EscapeCarriageReturnMessages()
        {
            BuildErrorEventArgs error = new BuildErrorEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");
            BuildWarningEventArgs warning = new BuildWarningEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");
            // Testing the method in Shared.EventArgsFormatting directly
            string errorString = EventArgsFormatting.FormatEventMessage(error, true);
            string warningString = EventArgsFormatting.FormatEventMessage(warning, true);
            string errorString2 = EventArgsFormatting.FormatEventMessage(error, false);
            string warningString2 = EventArgsFormatting.FormatEventMessage(warning, false);
            Assertion.AssertEquals("source.cs(42): CS error 312: message\\r Hello", errorString);
            Assertion.AssertEquals("source.cs(42): CS warning 312: message\\r Hello", warningString);

            Assertion.AssertEquals("source.cs(42): CS error 312: message\r Hello", errorString2);
            Assertion.AssertEquals("source.cs(42): CS warning 312: message\r Hello", warningString2);
        }

        [Test]
        public void ExactLocationFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 233, 236, 4, 8, 0);
            Assertion.AssertEquals(
                    "source.cs(233,4,236,8): CS error 312: Missing ;", s);

        }

        [Test]
        public void NullMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      null, "312", "source.cs", 233, 236, 4, 8, 0);
            // No exception was thrown

        }
    }
}

