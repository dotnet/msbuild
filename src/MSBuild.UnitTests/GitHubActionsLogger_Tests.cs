// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Logging.CICDLogger.GitHubActions;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    public class GitHubActionsLogger_Tests : IEventSource, IDisposable
    {
        private readonly StringWriter _outputWriter = new();
        private readonly GitHubActionsLogger _logger;

        public GitHubActionsLogger_Tests()
        {
            _logger = new GitHubActionsLogger();
            _logger.Initialize(this);
        }

        public void Dispose()
        {
            _logger.Shutdown();
            _outputWriter.Dispose();
        }

        [Fact]
        public void IsEnabled_WhenGitHubActionsEnvVarIsTrue_ReturnsTrue()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("GITHUB_ACTIONS", "true");

            GitHubActionsLogger.IsEnabled().ShouldBeTrue();
        }

        [Fact]
        public void IsEnabled_WhenGitHubActionsEnvVarIs1_ReturnsTrue()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("GITHUB_ACTIONS", "1");

            GitHubActionsLogger.IsEnabled().ShouldBeTrue();
        }

        [Fact]
        public void IsEnabled_WhenGitHubActionsEnvVarIsNotSet_ReturnsFalse()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("GITHUB_ACTIONS", string.Empty);

            GitHubActionsLogger.IsEnabled().ShouldBeFalse();
        }

        [Fact]
        public void ErrorRaised_WithFileAndLineInfo_FormatsCorrectly()
        {
            var error = new BuildErrorEventArgs(
                subcategory: null,
                code: "CS0103",
                file: "test.cs",
                lineNumber: 10,
                columnNumber: 5,
                endLineNumber: 10,
                endColumnNumber: 15,
                message: "The name 'foo' does not exist in the current context",
                helpKeyword: null!,
                senderName: null);

            ErrorRaised?.Invoke(this, error);

            string output = _outputWriter.ToString();
            output.ShouldContain("::error file=test.cs,line=10,col=5,endColumn=15,title=CS0103::The name 'foo' does not exist in the current context");
        }

        [Fact]
        public void ErrorRaised_WithoutFileInfo_FormatsCorrectly()
        {
            var error = new BuildErrorEventArgs(
                subcategory: null,
                code: "MSB1234",
                file: null,
                lineNumber: 0,
                columnNumber: 0,
                endLineNumber: 0,
                endColumnNumber: 0,
                message: "General build error",
                helpKeyword: null!,
                senderName: null);

            ErrorRaised?.Invoke(this, error);

            string output = _outputWriter.ToString();
            output.ShouldContain("::error,title=MSB1234::General build error");
        }

        [Fact]
        public void WarningRaised_WithFileAndLineInfo_FormatsCorrectly()
        {
            var warning = new BuildWarningEventArgs(
                subcategory: null,
                code: "CS0168",
                file: "test.cs",
                lineNumber: 20,
                columnNumber: 8,
                endLineNumber: 20,
                endColumnNumber: 12,
                message: "The variable 'bar' is declared but never used",
                helpKeyword: null!,
                senderName: null);

            WarningRaised?.Invoke(this, warning);

            string output = _outputWriter.ToString();
            output.ShouldContain("::warning file=test.cs,line=20,col=8,endColumn=12,title=CS0168::The variable 'bar' is declared but never used");
        }

        [Fact]
        public void MessageRaised_WithHighImportance_OutputsMessage()
        {
            _logger.Verbosity = LoggerVerbosity.Normal;

            var message = new BuildMessageEventArgs(
                message: "Building project...",
                helpKeyword: null!,
                senderName: null,
                importance: MessageImportance.High);

            MessageRaised?.Invoke(this, message);

            string output = _outputWriter.ToString();
            output.ShouldContain("Building project...");
        }

        [Fact]
        public void MessageRaised_WithLowImportanceAndNormalVerbosity_DoesNotOutput()
        {
            _logger.Verbosity = LoggerVerbosity.Normal;

            var message = new BuildMessageEventArgs(
                message: "Low importance message",
                helpKeyword: null!,
                senderName: null,
                importance: MessageImportance.Low);

            MessageRaised?.Invoke(this, message);

            string output = _outputWriter.ToString();
            output.ShouldBeEmpty();
        }

        [Fact]
        public void ProjectStarted_CreatesGroupCommand()
        {
            var projectStarted = new ProjectStartedEventArgs(
                message: null!,
                helpKeyword: null!,
                projectFile: "/src/MyProject.csproj",
                targetNames: "Build",
                properties: null!,
                items: null!);

            ProjectStarted?.Invoke(this, projectStarted);

            string output = _outputWriter.ToString();
            output.ShouldContain("::group::Building /src/MyProject.csproj");
        }

        [Fact]
        public void ProjectFinished_CreatesEndGroupCommand()
        {
            var projectFinished = new ProjectFinishedEventArgs(
                message: null!,
                helpKeyword: null!,
                projectFile: "/src/MyProject.csproj",
                succeeded: true);

            ProjectFinished?.Invoke(this, projectFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("::endgroup::");
        }

        [Fact]
        public void BuildFinished_WithSuccess_OutputsSuccessMessage()
        {
            _logger.Verbosity = LoggerVerbosity.Minimal;

            var buildFinished = new BuildFinishedEventArgs(
                message: "Build succeeded",
                helpKeyword: null!,
                succeeded: true);

            BuildFinished?.Invoke(this, buildFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("Build succeeded.");
        }

        [Fact]
        public void BuildFinished_WithFailure_OutputsFailureMessage()
        {
            _logger.Verbosity = LoggerVerbosity.Minimal;

            var buildFinished = new BuildFinishedEventArgs(
                message: "Build failed",
                helpKeyword: null!,
                succeeded: false);

            BuildFinished?.Invoke(this, buildFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("Build failed.");
        }

        [Fact]
        public void EscapeProperty_HandlesSpecialCharacters()
        {
            var error = new BuildErrorEventArgs(
                subcategory: null,
                code: "TEST",
                file: "file:with:colons,and,commas",
                lineNumber: 1,
                columnNumber: 1,
                endLineNumber: 1,
                endColumnNumber: 1,
                message: "Test\nwith\nnewlines",
                helpKeyword: null!,
                senderName: null);

            ErrorRaised?.Invoke(this, error);

            string output = _outputWriter.ToString();
            output.ShouldContain("%3A"); // Escaped colon
            output.ShouldContain("%2C"); // Escaped comma
            output.ShouldContain("%0A"); // Escaped newline
        }

        #region IEventSource implementation

#pragma warning disable CS0067
        public event BuildMessageEventHandler? MessageRaised;
        public event BuildErrorEventHandler? ErrorRaised;
        public event BuildWarningEventHandler? WarningRaised;
        public event BuildStartedEventHandler? BuildStarted;
        public event BuildFinishedEventHandler? BuildFinished;
        public event ProjectStartedEventHandler? ProjectStarted;
        public event ProjectFinishedEventHandler? ProjectFinished;
        public event TargetStartedEventHandler? TargetStarted;
        public event TargetFinishedEventHandler? TargetFinished;
        public event TaskStartedEventHandler? TaskStarted;
        public event TaskFinishedEventHandler? TaskFinished;
        public event CustomBuildEventHandler? CustomEventRaised;
        public event BuildStatusEventHandler? StatusEventRaised;
        public event AnyEventHandler? AnyEventRaised;
#pragma warning restore CS0067

        #endregion
    }
}
