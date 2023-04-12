// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    public class ServerCommandTest
    {
        [Fact]
        public void WritePidFile_WorksAsExpected()
        {
            // Arrange
            var expectedProcessId = Process.GetCurrentProcess().Id;
            var expectedRzcPath = typeof(ServerCommand).Assembly.Location;
            var expectedFileName = $"rzc-{expectedProcessId}";
            var directoryPath = Path.Combine(Path.GetTempPath(), "RazorTest", Guid.NewGuid().ToString());
            var path = Path.Combine(directoryPath, expectedFileName);

            var pipeName = Guid.NewGuid().ToString();
            var server = GetServerCommand(pipeName);

            // Act & Assert
            try
            {
                using (var _ = server.WritePidFile(directoryPath))
                {
                    Assert.True(File.Exists(path));

                    // Make sure another stream can be opened while the write stream is still open.
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Write | FileShare.Delete))
                    using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                    {
                        var lines = reader.ReadToEnd().Split(Environment.NewLine);
                        Assert.Equal(new[] { expectedProcessId.ToString(CultureInfo.InvariantCulture), "rzc", expectedRzcPath, pipeName }, lines);
                    }
                }

                // Make sure the file is deleted on dispose.
                Assert.False(File.Exists(path));
            }
            finally
            {
                // Cleanup after the test.
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, recursive: true);
                }
            }
        }

        [Fact]
        public void GetPidFilePath_ReturnsCorrectDefaultPath()
        {
            // Arrange
            var expectedPath = Path.Combine(".dotnet", "pids", "build");

            // Act
            var directoryPath = ServerCommand.GetPidFilePath();

            // Assert
            Assert.EndsWith(expectedPath, directoryPath);
        }

        [Fact]
        public void GetPidFilePath_UsesEnvironmentVariablePathIfSpecified()
        {
            // Arrange
            var expectedPath = "/Some/directory/path/";
            Environment.SetEnvironmentVariable("DOTNET_BUILD_PIDFILE_DIRECTORY", expectedPath);
            try
            {
                // Act
                var directoryPath = ServerCommand.GetPidFilePath();

                // Assert
                Assert.Equal(expectedPath, directoryPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_BUILD_PIDFILE_DIRECTORY", "");
            }
        }

        private ServerCommand GetServerCommand(string pipeName = null)
        {
            var application = new Application(
                CancellationToken.None,
                Mock.Of<ExtensionAssemblyLoader>(),
                Mock.Of<ExtensionDependencyChecker>(),
                (path, properties) => MetadataReference.CreateFromFile(path, properties));

            return new ServerCommand(application, pipeName);
        }
    }
}
