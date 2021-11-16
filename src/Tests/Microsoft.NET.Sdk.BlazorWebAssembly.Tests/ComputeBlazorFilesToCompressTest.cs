// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
    public class ComputeBlazorFilesToCompressTest
    {
        public ComputeBlazorFilesToCompressTest()
        {
            Directory.CreateDirectory(Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ComputeBlazorFilesToCompressTest)));
            ItemSpec = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ComputeBlazorFilesToCompressTest), Guid.NewGuid().ToString("N") + ".tmp");
            OriginalItemSpec = Path.Combine(TestContext.Current.TestExecutionDirectory, nameof(ComputeBlazorFilesToCompressTest), Guid.NewGuid().ToString("N") + ".tmp");
        }

        public string ItemSpec { get; }

        public string OriginalItemSpec { get; }

        [Fact]
        public void PrefersOriginalItemSpecWhenFileExists()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            File.Create(ItemSpec);
            File.Create(OriginalItemSpec);

            var asset = new StaticWebAsset()
            {
                Identity = ItemSpec,
                OriginalItemSpec = OriginalItemSpec
            };

            var task = new ComputeBlazorFilesToCompress
            {
                BuildEngine = buildEngine.Object,
                Assets = new[] { asset.ToTaskItem()}
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.AssetsToCompress.Should().HaveCount(1);
            task.AssetsToCompress[0].ItemSpec.Should().Be(OriginalItemSpec);
        }

        [Fact]
        public void FallsBackToItemSpecWhenIdentityDoesNotExist()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            File.Create(ItemSpec);

            var asset = new StaticWebAsset()
            {
                Identity = ItemSpec,
                OriginalItemSpec = OriginalItemSpec
            };

            var task = new ComputeBlazorFilesToCompress
            {
                BuildEngine = buildEngine.Object,
                Assets = new[] { asset.ToTaskItem() }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeTrue();
            task.AssetsToCompress.Should().HaveCount(1);
            task.AssetsToCompress[0].ItemSpec.Should().Be(ItemSpec);
        }

        [Fact]
        public void FailsWhenNeitherIdentityNorOriginalItemSpecExist()
        {
            // Arrange
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var asset = new StaticWebAsset()
            {
                Identity = ItemSpec,
                OriginalItemSpec = OriginalItemSpec
            };

            var task = new ComputeBlazorFilesToCompress
            {
                BuildEngine = buildEngine.Object,
                Assets = new[] { asset.ToTaskItem() }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().BeFalse();
            task.AssetsToCompress.Should().BeEmpty();
        }
    }
}
