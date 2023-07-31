// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAnUnpublishableProject : SdkTest
    {
        public GivenThatWeWantToPublishAnUnpublishableProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_does_not_publish_to_the_publish_folder()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("Unpublishable")
                .WithSource();

            var publishCommand = new PublishCommand(helloWorldAsset);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().NotExist();
        }
    }
}
