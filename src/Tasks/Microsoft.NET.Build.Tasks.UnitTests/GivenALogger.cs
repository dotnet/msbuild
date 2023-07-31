// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

            logger.LogMessage("Normal importance by default {0} {1} {2}", "a", "b", "c");
            logger.LogMessage(MessageImportance.Low, "Low importance {0} {1} {2}", "x", "y", "z");
            logger.LogMessage(MessageImportance.Normal, "Explicit normal importance");
            logger.LogMessage(MessageImportance.High, "High importance");

            logger.Messages.Should().Equal(
                new Message(MessageLevel.NormalImportance, "Normal importance by default a b c"),
                new Message(MessageLevel.LowImportance, "Low importance x y z"),
                new Message(MessageLevel.NormalImportance, "Explicit normal importance"),
                new Message(MessageLevel.HighImportance, "High importance"));
        }

        [Fact]
        public void ItIndicatesIfErrorsWereLogged()
        {
            var logger = new TestLogger();
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogWarning("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogMessage("_");
            logger.HasLoggedErrors.Should().BeFalse();

            logger.LogError("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();

            logger.LogWarning("NETSDK0000: _");
            logger.HasLoggedErrors.Should().BeTrue();

            logger.LogMessage("_");
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
            logWithoutErrorCode.Should().Throw<ArgumentException>();
#else
            logWithoutErrorCode();
            logger.Messages.Should().Equal(
                new Message(MessageLevel.Error, "No error code", code: default));
#endif
        }
    }
}

