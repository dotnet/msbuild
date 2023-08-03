// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Pack.Tests
{
    public class GivenThatWeWantToPackASimpleLibrary : SdkTest
    {
        public GivenThatWeWantToPackASimpleLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("17.1.0.60101")]
        public void It_packs_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource();

            var packCommand = new PackCommand(testAsset);

            packCommand
                .Execute()
                .Should()
                .Pass();

            var packageDirectory = packCommand.GetPackageDirectory();
            packageDirectory.Should().OnlyHaveFiles(new[]
            {
                "HelloWorld.1.0.0.nupkg",
            }, SearchOption.TopDirectoryOnly);

            var outputDirectory = packCommand.GetOutputDirectory();
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"HelloWorld.dll",
                $"HelloWorld.pdb",
                $"HelloWorld.deps.json",
                $"HelloWorld.runtimeconfig.json",
                $"HelloWorld{EnvironmentInfo.ExecutableExtension}",
            });
        }
    }
}
