// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    internal class EmptyLogger : Logger
    {
        /// <summary>
        /// Create a logger instance with a specific verbosity.
        /// </summary>
        /// <param name="verbosity">Verbosity level.</param>
        public EmptyLogger(LoggerVerbosity verbosity)
        {
            this.Verbosity = verbosity;
        }

        /// <summary>
        /// Subscribe to events.
        /// </summary>
        /// <param name="eventSource"></param>
        public override void Initialize(IEventSource eventSource)
        {
        }
    }

    [TestClass]
    public class Logger_Tests
    {
        [TestMethod]
        public void ExerciseMiscProperties()
        {
            EmptyLogger logger = new EmptyLogger(LoggerVerbosity.Diagnostic);
            logger.Parameters = "Parameters";
            Assert.IsTrue(string.Compare(logger.Parameters, "Parameters", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.AreEqual(LoggerVerbosity.Diagnostic, logger.Verbosity);
            logger.Shutdown();
        }
        /// <summary>
        /// Exercises every combination of the Logger.IsVerbosityAtLeast method.
        /// </summary>
        [TestMethod]
        public void IsVerbosityAtLeast()
        {
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.AreEqual(false,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.AreEqual(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));
        }
    }
}
