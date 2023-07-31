// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAResolveRuntimePackAssetsTask : SdkTest
    {
        public GivenAResolveRuntimePackAssetsTask(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFiltersSatelliteResources()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;

            var task = new ResolveRuntimePackAssets()
            {
                BuildEngine = new MockBuildEngine(),
                FrameworkReferences = new TaskItem[] { new TaskItem("TestFramework") },
                ResolvedRuntimePacks = new TaskItem[]
                {
                    new TaskItem("TestRuntimePack",
                    new Dictionary<string, string> {
                        { "FrameworkName", "TestFramework" },
                        { "RuntimeIdentifier", "test-rid" },
                        { "PackageDirectory", testDirectory },
                        { "PackageVersion", "0.1.0" },
                        { "IsTrimmable", "false" }
                    })
                },
                SatelliteResourceLanguages = new TaskItem[] { new TaskItem("de") }
            };

            Directory.CreateDirectory(Path.Combine(testDirectory, "data"));

            File.WriteAllText(
                Path.Combine(testDirectory, "data", "RuntimeList.xml"),
@"<FileList Name="".NET Core 3.1"" TargetFrameworkIdentifier="".NETCoreApp"" TargetFrameworkVersion=""3.1"" FrameworkName=""Microsoft.NETCore.App"">
  <File Type=""Resources"" Path=""runtimes/de/a.resources.dll"" Culture=""de"" FileVersion=""0.0.0.0"" />
  <File Type=""Resources"" Path=""runtimes/cs/a.resources.dll"" Culture=""cs"" FileVersion=""0.0.0.0"" />
</FileList>");

            task.Execute();
            task.RuntimePackAssets.Should().HaveCount(1);
            string expectedResource = Path.Combine("runtimes","de","a.resources.dll");
            task.RuntimePackAssets.FirstOrDefault().ItemSpec.Should().Contain(expectedResource);
        }
    }
}
