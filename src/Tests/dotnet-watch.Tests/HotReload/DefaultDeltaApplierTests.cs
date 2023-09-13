// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using Moq;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class DefaultDeltaApplierTests
    {
        [Fact]
        public void Initialize_ConfiguresEnvironmentVariables()
        {
            // Arrange
            var applier = new DefaultDeltaApplier(Mock.Of<IReporter>()) { SuppressNamedPipeForTests = true };
            var process = new ProcessSpec();
            var fileSet = new FileSet(null, new[]
            {
                new FileItem {  FilePath = "Test.cs" },
            });

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = true,
                ProcessSpec = process,
                FileSet = fileSet,
                Iteration = 0
            };

            // Act
            applier.Initialize(context, default);

            // Assert
            Assert.Equal("debug", process.EnvironmentVariables["DOTNET_MODIFIABLE_ASSEMBLIES"]);
            Assert.NotEmpty(process.EnvironmentVariables["DOTNET_HOTRELOAD_NAMEDPIPE_NAME"]);
            Assert.NotEmpty(process.EnvironmentVariables.DotNetStartupHooks);
        }
    }
}
