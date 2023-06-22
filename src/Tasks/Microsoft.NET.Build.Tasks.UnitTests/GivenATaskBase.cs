// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenATaskBase
    {
        private sealed class TestTask : TaskBase
        {
            protected override void ExecuteCore() {}
        }

        [Fact]
        public void ItRoutesLogMessagesToMSBuild()
        {
            var task = new TestTask();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;

            task.Log.Log(new Message(MessageLevel.HighImportance, "high", code: "code1", file: "file1"));
            task.Log.Log(new Message(MessageLevel.LowImportance, "low", code: "code2", file: "file2"));
            task.Log.Log(new Message(MessageLevel.NormalImportance, "normal", code: "code3", file: "file3"));
            task.Log.Log(new Message(MessageLevel.Warning, "warning", code: "code4", file: "file4"));
            task.Log.Log(new Message(MessageLevel.Error, "error", code: "code5", file: "file5"));

            engine.Messages.Count.Should().Be(3);
            engine.Errors.Count.Should().Be(1);
            engine.Warnings.Count.Should().Be(1);

            BuildMessageEventArgs message = engine.Messages[0];
            message.Importance.Should().Be(MessageImportance.High);
            message.Message.Should().Be("high");
            message.Code.Should().Be("code1");
            message.File.Should().Be("file1");

            message = engine.Messages[1];
            message.Importance.Should().Be(MessageImportance.Low);
            message.Message.Should().Be("low");
            message.Code.Should().Be("code2");
            message.File.Should().Be("file2");

            message = engine.Messages[2];
            message.Importance.Should().Be(MessageImportance.Normal);
            message.Message.Should().Be("normal");
            message.Code.Should().Be("code3");
            message.File.Should().Be("file3");

            BuildWarningEventArgs warning = engine.Warnings[0];
            warning.Message.Should().Be("warning");
            warning.Code.Should().Be("code4");
            warning.File.Should().Be("file4");

            BuildErrorEventArgs error = engine.Errors[0];
            error.Message.Should().Be("error");
            error.Code.Should().Be("code5");
            error.File.Should().Be("file5");
        }
    }
}
