// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tools
{
    public class NoRestoreFilterTest
    {
        private readonly string[] _arguments = new[] { "run" };

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedOnFirstRun()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Same(_arguments, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfMsBuildRevaluationIsRequired()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Test.proj" };
            context.RequiresMSBuildRevaluation = true;
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Same(_arguments, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_LeavesArgumentsUnchangedIfOptimizationIsSuppressed()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                },
                SuppressMSBuildIncrementalism = true,
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Program.cs" };
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Same(_arguments, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = _arguments,
                }
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Program.cs" };
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Equal(new[] { "run", "--no-restore" }, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_WithAdditionalArguments()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = new[] { "run", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar" },
                }
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Program.cs" };
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Equal(new[] { "run", "--no-restore", "-f", ToolsetInfo.CurrentTargetFramework, "--", "foo=bar" }, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_AddsNoRestoreSwitch_ForTestCommand()
        {
            // Arrange
            var filter = new NoRestoreFilter();

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = new[] { "test", "--filter SomeFilter" },
                }
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Program.cs" };
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Equal(new[] { "test", "--no-restore", "--filter SomeFilter" }, context.ProcessSpec.Arguments);
        }

        [Fact]
        public async Task ProcessAsync_DoesNotModifyArgumentsForUnknownCommands()
        {
            // Arrange
            var filter = new NoRestoreFilter();
            var arguments = new[] { "ef", "database", "update" };

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = false,
                Iteration = 0,
                ProcessSpec = new ProcessSpec
                {
                    Arguments = arguments,
                }
            };
            await filter.ProcessAsync(context, default);

            context.ChangedFile = new FileItem { FilePath = "Program.cs" };
            context.Iteration++;

            // Act
            await filter.ProcessAsync(context, default);

            // Assert
            Assert.Same(arguments, context.ProcessSpec.Arguments);
        }
    }
}
