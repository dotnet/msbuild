// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class EventArgsFormattingTests
    {
        [TestMethod]
        public void NoLineInfoFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 0, 0, 0);
            Assert.AreEqual(
                      "source.cs : CS error 312: Missing ;", s);
        }

        // Valid forms for line/col number patterns:
        // (line) or (line-line) or (line,col) or (line,col-col) or (line,col,line,col)
        [TestMethod]
        public void LineNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 0, 0);
            Assert.AreEqual(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void ColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 0, 0, 1, 2, 0);
            Assert.AreEqual(
                      "source.cs : CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void LineAndColumnNumberRange()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 4, 0);
            Assert.AreEqual(
                      "source.cs(1,3,2,4): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void LineAndColumnNumberRange2()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 3, 4, 0);
            Assert.AreEqual(
                      "source.cs(1,3-4): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void LineAndColumnNumberRange3()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 3, 0, 0);
            Assert.AreEqual(
                      "source.cs(1-2,3): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void LineAndColumnNumberRange4()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 2, 0, 3, 0);
            Assert.AreEqual(
                      "source.cs(1-2): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void LineAndColumnNumberRange5()
        {
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 1, 0, 2, 0, 0);
            Assert.AreEqual(
                      "source.cs(1,2): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void BasicFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 42, 0, 0, 0, 0);
            Assert.AreEqual(
                      "source.cs(42): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void EscapeCarriageReturnMessages()
        {
            BuildErrorEventArgs error = new BuildErrorEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");
            BuildWarningEventArgs warning = new BuildWarningEventArgs("CS", "312", "source.cs", 42, 0, 0, 0, "message\r Hello", "help", "sender");

            // Testing the method in Shared.EventArgsFormatting directly
            string errorString = EventArgsFormatting.FormatEventMessage(error, true);
            string warningString = EventArgsFormatting.FormatEventMessage(warning, true);
            string errorString2 = EventArgsFormatting.FormatEventMessage(error, false);
            string warningString2 = EventArgsFormatting.FormatEventMessage(warning, false);

            Assert.AreEqual("source.cs(42): CS error 312: message\\r Hello", errorString);
            Assert.AreEqual("source.cs(42): CS warning 312: message\\r Hello", warningString);

            Assert.AreEqual("source.cs(42): CS error 312: message\r Hello", errorString2);
            Assert.AreEqual("source.cs(42): CS warning 312: message\r Hello", warningString2);
        }

        [TestMethod]
        public void ExactLocationFormatEventMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      "Missing ;", "312", "source.cs", 233, 236, 4, 8, 0);
            Assert.AreEqual(
                    "source.cs(233,4,236,8): CS error 312: Missing ;", s);
        }

        [TestMethod]
        public void NullMessage()
        {
            // Testing the method in Shared.EventArgsFormatting directly
            string s = EventArgsFormatting.FormatEventMessage("error", "CS",
                      null, "312", "source.cs", 233, 236, 4, 8, 0);
            // No exception was thrown

        }

        /// <summary>
        /// Mainline test FormatEventMessage(BuildErrorEvent) 's common case
        /// </summary>
        [TestMethod]
        public void FormatEventMessageOnBEEA()
        {
            MyLogger l = new MyLogger();
            BuildErrorEventArgs beea = new BuildErrorEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");
            string s = l.FormatErrorEvent(beea);
            Assert.AreEqual(
               "file.vb(42): VBC error 31415: Some long message", s);
        }

        /// <summary>
        /// Mainline test FormatEventMessage(BuildWarningEvent) 's common case
        /// </summary>
        [TestMethod]
        public void FormatEventMessageOnBWEA()
        {
            MyLogger l = new MyLogger();
            BuildWarningEventArgs bwea = new BuildWarningEventArgs("VBC",
                        "31415", "file.vb", 42, 0, 0, 0,
                        "Some long message", "help", "sender");
            string s = l.FormatWarningEvent(bwea);
            Assert.AreEqual(
               "file.vb(42): VBC warning 31415: Some long message", s);
        }

        /// <summary>
        /// Check null handling
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FormatEventMessageOnNullBEEA()
        {
            MyLogger l = new MyLogger();
            BuildErrorEventArgs beea = null;
            string s = l.FormatErrorEvent(beea);
        }

        /// <summary>
        /// Check null handling
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void FormatEventMessageOnNullBWEA()
        {
            MyLogger l = new MyLogger();
            BuildWarningEventArgs bwea = null;
            string s = l.FormatWarningEvent(bwea);
        }
    }

    /// <summary>
    /// Minimal logger implementation
    /// </summary>
    internal class MyLogger : Logger
    {
        public override void Initialize(IEventSource eventSource)
        {
            // do nothing
        }
    }
}

