// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework.Utilities;
using Xunit;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class WindowsEnvironmentPathTests
    {
        public WindowsEnvironmentPathTests()
        {
            _reporter = new BufferedReporter();
            _mockPathInternal = new MockPathInternal();
            var mockEnvironmentProvider = new MockEnvironmentProvider(_mockPathInternal);
            var mockEnvironmentPathEditor = new MockEnvironmentPathEditor(_mockPathInternal);
            _windowsEnvironmentPath = new WindowsEnvironmentPath(
                _toolsPath,
                @"%USERPROFILE%\.dotnet\tools",
                mockEnvironmentProvider,
                mockEnvironmentPathEditor,
                _reporter
            );
        }

        private readonly BufferedReporter _reporter;
        private readonly WindowsEnvironmentPath _windowsEnvironmentPath;
        private readonly MockPathInternal _mockPathInternal;
        private const string _toolsPath = @"C:\Users\username\.dotnet\tools";

        [Fact]
        public void GivenPathIsNullItItAddsToEnvironment()
        {
            _mockPathInternal.UserLevelPath = null;

            _windowsEnvironmentPath.AddPackageExecutablePathToUserPath();

            _reporter.Lines.Should().BeEmpty();
            _mockPathInternal.UserLevelPath.Should().Be(@"%USERPROFILE%\.dotnet\tools");
        }

        [Fact]
        public void GivenPathNotSetInProcessItPrintsReopenNoticeAndNoChangeInEnvironment()
        {
            _mockPathInternal.UserLevelPath = @"%USERPROFILE%\.dotnet\tools";

            _windowsEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            _reporter.Lines.Should().Equal(CommonLocalizableStrings.EnvironmentPathWindowsNeedReopen);
        }

        [Fact]
        public void GivenPathNotSetInProcessWhenAddPackageExecutablePathToUserPathItPrintsReopenNoticeAndNoChangeInEnvironment()
        {
            _mockPathInternal.UserLevelPath = @"%USERPROFILE%\Other;%USERPROFILE%\.dotnet\tools";

            _windowsEnvironmentPath.AddPackageExecutablePathToUserPath();

            _reporter.Lines.Should().BeEmpty("No message since this happens in first run experience");
            _mockPathInternal.UserLevelPath.Should()
                .Be(@"%USERPROFILE%\Other;%USERPROFILE%\.dotnet\tools", "no change");
        }

        [Fact]
        public void GivenPathNotSetItAddsToEnvironment()
        {
            _mockPathInternal.UserLevelPath = @"%USERPROFILE%\Other";

            _windowsEnvironmentPath.AddPackageExecutablePathToUserPath();

            _reporter.Lines.Should().BeEmpty();
            _mockPathInternal.UserLevelPath.Should().Be(@"%USERPROFILE%\Other;%USERPROFILE%\.dotnet\tools");
        }

        [Fact]
        public void GivenPathNotSetItPrintsManualInstructions()
        {
            _mockPathInternal.UserLevelPath = @"%USERPROFILE%\Other";
            _windowsEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            _reporter.Lines.Should().Equal(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathWindowsManualInstructions,
                    _toolsPath));
        }

        [Fact]
        public void GivenPathSetInProcessAndEnvironmentItPrintsNothingAndNoChangeInEnvironment()
        {
            var pathWithToolPath = @"%USERPROFILE%\Other;%USERPROFILE%\.dotnet\tools";
            _mockPathInternal.UserLevelPath = pathWithToolPath;
            _mockPathInternal.ProcessLevelPath = pathWithToolPath;

            _windowsEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            _reporter.Lines.Should().BeEmpty();
        }

        [Fact]
        public void GivenPathSetItDoesNotAddPathToEnvironment()
        {
            var pathWithToolPath = @"%USERPROFILE%\Other;%USERPROFILE%\.dotnet\tools";
            _mockPathInternal.UserLevelPath = pathWithToolPath;
            _mockPathInternal.ProcessLevelPath = pathWithToolPath;

            _windowsEnvironmentPath.AddPackageExecutablePathToUserPath();

            _reporter.Lines.Should().BeEmpty();
            _mockPathInternal.UserLevelPath.Should().Be(pathWithToolPath, "no change");
        }

        private class MockPathInternal
        {
            public string MachineLevelPath { get; set; }
            public string UserLevelPath { get; set; }
            public string ProcessLevelPath { get; set; }
        }

        private class MockEnvironmentProvider : IEnvironmentProvider
        {
            private readonly MockPathInternal _mockPathInternal;

            public MockEnvironmentProvider(MockPathInternal mockPathInternal)
            {
                _mockPathInternal = mockPathInternal ?? throw new ArgumentNullException(nameof(mockPathInternal));
            }

            public IEnumerable<string> ExecutableExtensions { get; }

            public string GetCommandPath(string commandName, params string[] extensions)
            {
                throw new NotImplementedException();
            }

            public string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions)
            {
                throw new NotImplementedException();
            }

            public string GetCommandPathFromRootPath(string rootPath, string commandName,
                IEnumerable<string> extensions)
            {
                throw new NotImplementedException();
            }

            public bool GetEnvironmentVariableAsBool(string name, bool defaultValue)
            {
                throw new NotImplementedException();
            }

            public string GetEnvironmentVariable(string name)
            {
                throw new NotImplementedException();
            }

            public string GetEnvironmentVariable(string variable, EnvironmentVariableTarget target)
            {
                if (variable != "PATH")
                {
                    throw new ArgumentException("should only read PATH");
                }

                switch (target)
                {
                    case EnvironmentVariableTarget.Process:
                        return Expand(_mockPathInternal.ProcessLevelPath);
                    case EnvironmentVariableTarget.User:
                        return Expand(_mockPathInternal.UserLevelPath);
                    case EnvironmentVariableTarget.Machine:
                        return Expand(_mockPathInternal.MachineLevelPath);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(target), target, null);
                }
            }

            public void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target)
            {
                throw new NotImplementedException();
            }

            private static string Expand(string path)
            {
                return path?.Replace("%USERPROFILE%", @"C:\Users\username");
            }
        }

        private class MockEnvironmentPathEditor : IWindowsRegistryEnvironmentPathEditor
        {
            private readonly MockPathInternal _mockPathInternal;

            public MockEnvironmentPathEditor(MockPathInternal mockPathInternal)
            {
                _mockPathInternal = mockPathInternal;
            }

            public string Get(SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
            {
                switch (sdkEnvironmentVariableTarget)
                {
                    case SdkEnvironmentVariableTarget.DotDefault:
                        return "";
                    case SdkEnvironmentVariableTarget.CurrentUser:
                        return _mockPathInternal.UserLevelPath;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sdkEnvironmentVariableTarget),
                            sdkEnvironmentVariableTarget, null);
                }
            }

            public void Set(string value, SdkEnvironmentVariableTarget sdkEnvironmentVariableTarget)
            {
                switch (sdkEnvironmentVariableTarget)
                {
                    case SdkEnvironmentVariableTarget.DotDefault:
                        throw new InvalidOperationException("Should never touch DotDefault's EnvironmentVariable.");
                    case SdkEnvironmentVariableTarget.CurrentUser:
                        _mockPathInternal.UserLevelPath = value;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(sdkEnvironmentVariableTarget),
                            sdkEnvironmentVariableTarget, null);
                }
            }
        }
    }
}
