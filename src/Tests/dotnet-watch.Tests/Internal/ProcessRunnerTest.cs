// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using Xunit;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class ProcessRunnerTest
    {
        [Fact]
        public void SetEnvironmentVariable_SetsSpecifiedValue()
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo();

           // Act
           ProcessRunner.SetEnvironmentVariable(processStartInfo, "Test", new () { "value1" }, ';', _ => null);

            // Assert
            Assert.Equal("value1", processStartInfo.Environment["Test"]);
        }

        [Fact]
        public void SetEnvironmentVariable_ConcatenatesMultipleValues()
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo();

            // Act
            ProcessRunner.SetEnvironmentVariable(processStartInfo, "Test", new() { "value1", "value2" }, ';', _ => null);

            // Assert
            Assert.Equal("value1;value2", processStartInfo.Environment["Test"]);
        }

        [Fact]
        public void SetEnvironmentVariable_Concatenates_WithEnvironmentVariable()
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo();

            // Act
            ProcessRunner.SetEnvironmentVariable(processStartInfo, "Test", new() { "value1", "value2" }, ';', _ => "value3");

            // Assert
            Assert.Equal("value3;value1;value2", processStartInfo.Environment["Test"]);
        }

        [Fact]
        public void SetEnvironmentVariable_Concatenates_WithEnvironmentVariableAndPreviouslyConfiguredValue()
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                Environment = { ["Test"] = "value4" },
            };
            
            // Act
            ProcessRunner.SetEnvironmentVariable(processStartInfo, "Test", new() { "value1", "value2" }, ';', _ => "value3");

            // Assert
            Assert.Equal("value3;value4;value1;value2", processStartInfo.Environment["Test"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void SetEnvironmentVariable_IgnoresNullOrEmptyEnvironment(string value)
        {
            // Arrange
            var processStartInfo = new ProcessStartInfo
            {
                Environment = { ["Test"] = "value4" },
            };

            // Act
            ProcessRunner.SetEnvironmentVariable(processStartInfo, "Test", new() { "value1", "value2" }, ';', _ => value);

            // Assert
            Assert.Equal("value4;value1;value2", processStartInfo.Environment["Test"]);
        }
    }
}
