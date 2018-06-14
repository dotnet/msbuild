// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenALogger
    {
        private sealed class TestLogger : Logger
        {
            public List<Message> Messages { get; } = new List<Message>();
            protected override void LogCore(in Message message) => Messages.Add(message);
        }

        [Fact]
        public void ItLogsWarnings()
        {
            var logger = new TestLogger();

            logger.LogWarning("NETSDK1234: Hello, {0}!", "world");
            logger.LogWarning("NETSDK4567: Goodbye, {0} {1}.", "cruel", "world");

            logger.Messages.Should().Equal(
                new Message(MessageLevel.Warning, "Hello, world!", code: "NETSDK1234"),
                new Message(MessageLevel.Warning, "Goodbye, cruel world.", code: "NETSDK4567"));
        }

        [Fact]
        public void ItLogsErrors()
        {
            var logger = new TestLogger();

            logger.LogError("NETSDK9898: Uh oh! {0}", ":(");

            logger.Messages.Should().Equal(
                new Message(MessageLevel.Error, "Uh oh! :(", code: "NETSDK9898"));
        }

        [Fact]
        public void ItLogsMessages()
        {
            var logger = new TestLogger();

            logger.LogMessage("NETSDK9876: Normal importance by default {0} {1} {2}", "a", "b", "c");
            logger.LogMessage(MessageImportance.Low, "NETSDK1111: Low importance {0} {1} {2}", "x", "y", "z");
            logger.LogMessage(MessageImportance.Normal, "NETSDK2222: Explicit normal importance");
            logger.LogMessage(MessageImportance.High, "NETSDK3333: High importance");

            logger.Messages.Should().Equal(
                new Message(MessageLevel.NormalImportance, "Normal importance by default a b c", code: "NETSDK9876"),
                new Message(MessageLevel.LowImportance, "Low importance x y z", code: "NETSDK1111"),
                new Message(MessageLevel.NormalImportance, "Explicit normal importance", code: "NETSDK2222"),
                new Message(MessageLevel.HighImportance, "High importance", "NETSDK3333"));
        }

        [Fact]
        public void ItIndicatesIfErrorsWereLogged()
        {
            var logger = new TestLogger();
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogWarning("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogMessage("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogError("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();

            logger.LogWarning("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();

            logger.LogMessage("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();

            logger.LogError("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();
        }

        [Fact]
        public void ItEnforcesErrorCodesInDebug()
        {
            var logger = new TestLogger();
            Action logWithoutErrorCode = () => logger.LogError("No error code");

#if DEBUG
            logWithoutErrorCode.ShouldThrow<ArgumentException>();
#else
            logWithoutErrorCode();
            logger.Messages.Should().Equal(
                new Message(MessageLevel.Error, "No error code", code: default));
#endif
        }
    }
}

