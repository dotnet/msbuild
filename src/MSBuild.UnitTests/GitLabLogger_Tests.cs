// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Logging.CICDLogger.GitLab;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests
{
    public class GitLabLogger_Tests : IEventSource, IDisposable
    {
        private readonly StringWriter _outputWriter = new();
        private readonly GitLabLogger _logger;

        public GitLabLogger_Tests()
        {
            _logger = new GitLabLogger();
            _logger.Initialize(this);
        }

        public void Dispose()
        {
            _logger.Shutdown();
            _outputWriter.Dispose();
        }

        [Fact]
        public void IsEnabled_WhenGitLabCIEnvVarIsSet_ReturnsTrue()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("GITLAB_CI", "true");

            GitLabLogger.IsEnabled().ShouldBeTrue();
        }

        [Fact]
        public void IsEnabled_WhenGitLabCIEnvVarIsNotSet_ReturnsFalse()
        {
            using TestEnvironment testEnvironment = TestEnvironment.Create();
            testEnvironment.SetEnvironmentVariable("GITLAB_CI", string.Empty);

            GitLabLogger.IsEnabled().ShouldBeFalse();
        }

        [Fact]
        public void ErrorRaised_WithFileAndLineInfo_FormatsWithRedColor()
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
            output.ShouldContain("\x1b[31m"); // Red color code
            output.ShouldContain("ERROR:");
            output.ShouldContain("test.cs(10,5)");
            output.ShouldContain("CS0103");
            output.ShouldContain("The name 'foo' does not exist in the current context");
            output.ShouldContain("\x1b[0m"); // Reset color code
        }

        [Fact]
        public void ErrorRaised_WithoutFileInfo_FormatsWithRedColor()
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
            output.ShouldContain("\x1b[31m"); // Red color code
            output.ShouldContain("ERROR:");
            output.ShouldContain("MSB1234");
            output.ShouldContain("General build error");
            output.ShouldContain("\x1b[0m"); // Reset color code
        }

        [Fact]
        public void WarningRaised_WithFileAndLineInfo_FormatsWithYellowColor()
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
            output.ShouldContain("\x1b[33m"); // Yellow color code
            output.ShouldContain("WARNING:");
            output.ShouldContain("test.cs(20,8)");
            output.ShouldContain("CS0168");
            output.ShouldContain("The variable 'bar' is declared but never used");
            output.ShouldContain("\x1b[0m"); // Reset color code
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
        public void ProjectStarted_CreatesCollapsibleSection()
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
            output.ShouldContain("\x1b[0Ksection_start:"); // Section start marker
            output.ShouldContain("build_project_1"); // Section name
            output.ShouldContain("\x1b[36m"); // Cyan color
            output.ShouldContain("Building /src/MyProject.csproj");
            output.ShouldContain("\x1b[0m"); // Reset color
        }

        [Fact]
        public void ProjectFinished_EndsCollapsibleSection()
        {
            // Start a project first
            var projectStarted = new ProjectStartedEventArgs(
                message: null!,
                helpKeyword: null!,
                projectFile: "/src/MyProject.csproj",
                targetNames: "Build",
                properties: null!,
                items: null!);
            ProjectStarted?.Invoke(this, projectStarted);

            _outputWriter.GetStringBuilder().Clear();

            var projectFinished = new ProjectFinishedEventArgs(
                message: null!,
                helpKeyword: null!,
                projectFile: "/src/MyProject.csproj",
                succeeded: true);

            ProjectFinished?.Invoke(this, projectFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("\x1b[0Ksection_end:"); // Section end marker
            output.ShouldContain("build_project_1"); // Section name
        }

        [Fact]
        public void BuildFinished_WithSuccess_OutputsGreenSuccessMessage()
        {
            _logger.Verbosity = LoggerVerbosity.Minimal;

            var buildFinished = new BuildFinishedEventArgs(
                message: "Build succeeded",
                helpKeyword: null!,
                succeeded: true);

            BuildFinished?.Invoke(this, buildFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("\x1b[32m"); // Green color code
            output.ShouldContain("Build succeeded.");
            output.ShouldContain("\x1b[0m"); // Reset color code
        }

        [Fact]
        public void BuildFinished_WithFailure_OutputsRedFailureMessage()
        {
            _logger.Verbosity = LoggerVerbosity.Minimal;

            var buildFinished = new BuildFinishedEventArgs(
                message: "Build failed",
                helpKeyword: null!,
                succeeded: false);

            BuildFinished?.Invoke(this, buildFinished);

            string output = _outputWriter.ToString();
            output.ShouldContain("\x1b[31m"); // Red color code
            output.ShouldContain("Build failed.");
            output.ShouldContain("\x1b[0m"); // Reset color code
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
