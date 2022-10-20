// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileLibrary : SdkTest
    {
        public GivenThatWeWantToPublishASingleFileLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        // Tests regression on https://github.com/dotnet/sdk/pull/28484
        public void ItPublishesSuccessfullyWithRIDAndPublishSingleFileLibrary()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var testAsset = _testAssetsManager
                     .CopyTestAsset("AppWithLibrarySDKStyleThatPublishesSingleFile")
                     .WithTargetFramework(targetFramework)
                     .WithSource();

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute()
                    .Should()
                    .Pass();

            // It would be better if we could somehow check the library binlog or something for a RID instead.
            var exeFolder = publishCommand.GetOutputDirectory(targetFramework: targetFramework);
            // Parent: RID, then TFM, then Debug, then bin, then the test folder
            var ridlessLibraryDllPath = Path.Combine(exeFolder.Parent.Parent.Parent.Parent.FullName, "lib", "bin", "Debug", targetFramework, "lib.dll");
            Assert.True(File.Exists(ridlessLibraryDllPath));
        }

    }

}
