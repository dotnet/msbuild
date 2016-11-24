// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenThatWeWantToReadLockFilesQuickly : TestBase
    {
        [Fact]
        public void ItFailsInLessThanOneSecondWhenTheProjectAssetsJsonDoesNotExist()
        {
            var testInstance = TestAssets.Get("TestAppWithProjDepTool")
                .CreateInstance()
                .WithSourceFiles();

            var assetsFile = testInstance.Root.GetDirectory("obj").GetFile("project.assets.json").FullName;
            var expectedMessage = string.Join(
                Environment.NewLine,
                $"File not found `{assetsFile}`.",
                "The project may not have been restored or restore failed - run `dotnet restore`");

            Action action = () =>
            {
                var lockFile = new LockFileFormat()
                    .ReadWithLock(assetsFile)
                    .Result;
            };

            var stopWatch = Stopwatch.StartNew();

            action.ShouldThrow<GracefulException>().WithMessage(expectedMessage);

            stopWatch.Stop();
            stopWatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        }
    }
}