// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAGenerateRuntimeConfigurationFiles
    {
        private readonly string _runtimeConfigPath;
        private readonly string _runtimeConfigDevPath;

        public GivenAGenerateRuntimeConfigurationFiles()
        {
            string testTempDir = Path.Combine(Path.GetTempPath(), "dotnetSdkTests");
            Directory.CreateDirectory(testTempDir);
            _runtimeConfigPath =
                Path.Combine(testTempDir, nameof(ItCanGenerateWithoutAssetFile) + "runtimeconfig.json");
            _runtimeConfigDevPath =
                Path.Combine(testTempDir, nameof(ItCanGenerateWithoutAssetFile) + "runtimeconfig.dev.json");
            if (File.Exists(_runtimeConfigPath))
            {
                File.Delete(_runtimeConfigPath);
            }

            if (File.Exists(_runtimeConfigDevPath))
            {
                File.Delete(_runtimeConfigDevPath);
            }
        }

        [Fact]
        public void ItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v3.0",
                TargetFramework = "netcoreapp3.0",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", "3.0.0-preview1.100"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.ShouldNotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    @"{
  ""runtimeOptions"": {
    ""tfm"": ""netcoreapp3.0"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""3.0.0-preview1.100""
    }
  }
}");
            File.Exists(_runtimeConfigDevPath).Should().BeFalse("No nuget involved, so no extra probing path");
        }


        [Fact]
        public void Given3RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v3.0",
                TargetFramework = "netcoreapp3.0",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", "3.1.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", "3.1.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.AspNetCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.AspNetCore.App"}, {"Version", "3.1.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.ShouldNotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    @"{
  ""runtimeOptions"": {
    ""tfm"": ""netcoreapp3.0"",
    ""rollForward"": ""LatestMinor"",
    ""frameworks"": [
      {
        ""name"": ""Microsoft.WindowsDesktop.App"",
        ""version"": ""3.1.0""
      },
      {
        ""name"": ""Microsoft.AspNetCore.App"",
        ""version"": ""3.1.0""
      }
    ]
  }
}",
                    "There is no Microsoft.NETCore.App. And it is under frameworkS.");
        }

        [Fact]
        public void Given2RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = ".NETCoreApp,Version=v3.0",
                TargetFramework = "netcoreapp3.0",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", "3.1.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", "3.1.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.ShouldNotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    @"{
  ""runtimeOptions"": {
    ""tfm"": ""netcoreapp3.0"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {
      ""name"": ""Microsoft.WindowsDesktop.App"",
      ""version"": ""3.1.0""
    }
  }
}",
                    "There is no Microsoft.NETCore.App.");
        }


        private class TestableGenerateRuntimeConfigurationFiles : GenerateRuntimeConfigurationFiles
        {
            public void PublicExecuteCore()
            {
                base.ExecuteCore();
            }
        }
    }
}
