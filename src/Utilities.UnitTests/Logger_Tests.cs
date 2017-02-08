// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

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

    public class Logger_Tests
    {
        [Fact]
        public void ExerciseMiscProperties()
        {
            EmptyLogger logger = new EmptyLogger(LoggerVerbosity.Diagnostic);
            logger.Parameters = "Parameters";
            Assert.Equal(0, string.Compare(logger.Parameters, "Parameters", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(LoggerVerbosity.Diagnostic, logger.Verbosity);
            logger.Shutdown();
        }
        /// <summary>
        /// Exercises every combination of the Logger.IsVerbosityAtLeast method.
        /// </summary>
        [Fact]
        public void IsVerbosityAtLeast()
        {
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Quiet)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Minimal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Normal)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.Equal(false,
                (new EmptyLogger(LoggerVerbosity.Detailed)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));

            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Quiet));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Minimal));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Normal));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Detailed));
            Assert.Equal(true,
                (new EmptyLogger(LoggerVerbosity.Diagnostic)).IsVerbosityAtLeast(LoggerVerbosity.Diagnostic));
        }
    }
}
