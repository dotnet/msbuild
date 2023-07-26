// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.NET.TestFramework;
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
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}");
            File.Exists(_runtimeConfigDevPath).Should().BeFalse("No nuget involved, so no extra probing path");
        }


        [Fact]
        public void Given3RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.AspNetCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.AspNetCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""frameworks"": [
      {{
        ""name"": ""Microsoft.WindowsDesktop.App"",
        ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
      }},
      {{
        ""name"": ""Microsoft.AspNetCore.App"",
        ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
      }}
    ]
  }}
}}",
                    "There is no Microsoft.NETCore.App. And it is under frameworkS.");
        }

        [Fact]
        public void Given2RuntimeFrameworksItCanGenerateWithoutAssetFile()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    ),
                    new MockTaskItem(
                        "Microsoft.WindowsDesktop.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.WindowsDesktop.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.WindowsDesktop.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}",
                    "There is no Microsoft.NETCore.App.");
        }

        [Fact]
        public void GivenTargetMonikerItGeneratesShortName()
        {
            var task = new TestableGenerateRuntimeConfigurationFiles
            {
                BuildEngine = new MockNeverCacheBuildEngine4(),
                TargetFrameworkMoniker = $".NETCoreApp,Version=v{ToolsetInfo.CurrentTargetFrameworkVersion}",
                RuntimeConfigPath = _runtimeConfigPath,
                RuntimeConfigDevPath = _runtimeConfigDevPath,
                RuntimeFrameworks = new[]
                {
                    new MockTaskItem(
                        "Microsoft.NETCore.App",
                        new Dictionary<string, string>
                        {
                            {"FrameworkName", "Microsoft.NETCore.App"}, {"Version", $"{ToolsetInfo.CurrentTargetFrameworkVersion}.0"}
                        }
                    )
                },
                RollForward = "LatestMinor"
            };

            Action a = () => task.PublicExecuteCore();
            a.Should().NotThrow();

            File.ReadAllText(_runtimeConfigPath).Should()
                .Be(
                    $@"{{
  ""runtimeOptions"": {{
    ""tfm"": ""{ToolsetInfo.CurrentTargetFramework}"",
    ""rollForward"": ""LatestMinor"",
    ""framework"": {{
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""{ToolsetInfo.CurrentTargetFrameworkVersion}.0""
    }}
  }}
}}");
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
