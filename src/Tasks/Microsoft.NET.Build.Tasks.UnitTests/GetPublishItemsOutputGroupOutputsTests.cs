// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GetPublishItemsOutputGroupOutputsTests
    {
        private readonly MockTaskItem _apphost
            = new(@"C:\work\temp\WindowsDesktopSdkTest_without_ProjectSdk_set\obj\Debug\net5.0\apphost.exe",
                new Dictionary<string, string>
                {
                    {"RelativePath", "WindowsDesktopSdkTest_without_ProjectSdk_set.exe"},
                    {"IsKeyOutput", "True"},
                    {"CopyToPublishDirectory", "Always"},
                    {"TargetPath", "WindowsDesktopSdkTest_without_ProjectSdk_set.exe"},
                });


        private readonly MockTaskItem _dll =
            new(@"obj\Debug\net5.0\WindowsDesktopSdkTest_without_ProjectSdk_set.dll",
                new Dictionary<string, string>
                {
                    {"RelativePath", "WindowsDesktopSdkTest_without_ProjectSdk_set.dll"},
                    {"CopyToPublishDirectory", "Always"},
                });

        // The logic is cross platform but the test path examples are all in Windows
        [WindowsOnlyFact]
        public void It_can_expand_OutputPath()
        {
            var task = new GetPublishItemsOutputGroupOutputs
            {
                PublishDir = @"bin\Debug\net5.0\publish\",
                ResolvedFileToPublish = new[]
                {
                    _apphost, _dll, _dll
                }
            };

            task.Execute().Should().BeTrue();

            task.PublishItemsOutputGroupOutputs.Length.Should().Be(3);

            // mock itemgroup does not support well-known metadata "FullPath". So no assertion on that
            // https://github.com/dotnet/msbuild/blob/46b723ba9ee9f4297d0c8ccbb6dc52e4bd8ea438/src/Shared/Modifiers.cs#L53-L67
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("RelativePath").Should().Be("WindowsDesktopSdkTest_without_ProjectSdk_set.exe");
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("OutputGroup").Should().Be("PublishItemsOutputGroup");
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("IsKeyOutput").Should().Be("True");
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("OutputPath").Should().Be(@"bin\Debug\net5.0\publish\WindowsDesktopSdkTest_without_ProjectSdk_set.exe");
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("CopyToPublishDirectory").Should().Be("Always", "should keep all existing metadata");
            task.PublishItemsOutputGroupOutputs[0].GetMetadata("TargetPath").Should().Be("WindowsDesktopSdkTest_without_ProjectSdk_set.exe");

            task.PublishItemsOutputGroupOutputs[1].GetMetadata("RelativePath").Should().Be("WindowsDesktopSdkTest_without_ProjectSdk_set.dll");
            task.PublishItemsOutputGroupOutputs[1].GetMetadata("OutputGroup").Should().Be("PublishItemsOutputGroup");
            task.PublishItemsOutputGroupOutputs[1].GetMetadata("CopyToPublishDirectory").Should().Be("Always");
            task.PublishItemsOutputGroupOutputs[1].GetMetadata("TargetPath").Should().Be(@"WindowsDesktopSdkTest_without_ProjectSdk_set.dll");

            // duplicated item should have the same info without error
            task.PublishItemsOutputGroupOutputs[2].GetMetadata("RelativePath").Should().Be("WindowsDesktopSdkTest_without_ProjectSdk_set.dll");
            task.PublishItemsOutputGroupOutputs[2].GetMetadata("OutputGroup").Should().Be("PublishItemsOutputGroup");
            task.PublishItemsOutputGroupOutputs[2].GetMetadata("CopyToPublishDirectory").Should().Be("Always");
            task.PublishItemsOutputGroupOutputs[2].GetMetadata("TargetPath").Should().Be(@"WindowsDesktopSdkTest_without_ProjectSdk_set.dll");
        }
    }
}
