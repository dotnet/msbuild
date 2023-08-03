// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeWantToGenerateSupportedTargetFrameworkAlias
    {
        private static List<(string targetFrameworkMoniker, string displayName)> MockSupportedTargetFramework = new List<(string, string)>()
            {
                ( ".NETCoreApp,Version=v3.0", ".NET Core 3.0"),
                ( ".NETCoreApp,Version=v3.1", ".NET Core 3.1"),
                ( ".NETCoreApp,Version=v5.0", ".NET 5"),
                ( ".NETCoreApp,Version=v6.0", ".NET 6"),
                ( ".NETStandard,Version=v2.0", ".NET Standard 2.0"),
                ( ".NETStandard,Version=v2.1", ".NET Standard 2.1"),
                ( ".NETFramework,Version=v4.7.1", ".NET Framework 4.7.1"),
                ( ".NETFramework,Version=v4.8", ".NET Framework 4.8"),
            };

        [Fact]
        public void It_generates_supported_net_standard_target_framework_alias_items()
        {
            var targetFrameworkMoniker = ".NETStandard,Version=v2.1";
            RunTask(targetFrameworkMoniker, targetPlatformMoniker: string.Empty, UseWpf: false, UseWindowsForms: false, expectedResult: new List<(string, string)>
                {
                    ("netstandard2.0", ".NET Standard 2.0"),
                    ("netstandard2.1", ".NET Standard 2.1"),
                });
        }

        [Fact]
        public void It_generates_supported_net_framework_target_framework_alias_items()
        {
            var targetFrameworkMoniker = ".NETFramework,Version=v4.8.1";
            RunTask(targetFrameworkMoniker, targetPlatformMoniker: string.Empty, UseWpf: false, UseWindowsForms: false, expectedResult: new List<(string, string)>
                {
                    ("net471", ".NET Framework 4.7.1"),
                    ("net48", ".NET Framework 4.8.1")
                });
        }

        [Theory]
        [InlineData(".NETCoreApp,Version=v3.1")]
        [InlineData(".NETCoreApp,Version=v5.0")]
        [InlineData(".NETCoreApp,Version=v6.0")]
        public void It_generates_supported_net_core_target_framework_alias_items(string targetFrameworkMoniker)
        {
            RunTask(targetFrameworkMoniker, targetPlatformMoniker: string.Empty, UseWpf: false, UseWindowsForms: false, expectedResult: new List<(string, string)>
                {
                    ("netcoreapp3.0", ".NET Core 3.0"),
                    ("netcoreapp3.1", ".NET Core 3.1"),
                    ("net5.0", ".NET 5"),
                    ("net6.0", ".NET 6")
                });
        }

        [Theory]
        [InlineData(".NETCoreApp,Version=v5.0", "Windows,Version=7.0")]
        [InlineData(".netcoreapp,version=v5.0", "windows,version=7.0")]
        [InlineData(".NETCoreApp,Version=v6.0", "Windows,Version=7.0")]
        public void It_generates_supported_target_framework_alias_items_when_targeting_windows(string targetFrameworkMoniker, string targetPlatformMoniker)
        {
            RunTask(targetFrameworkMoniker, targetPlatformMoniker, UseWpf: false, UseWindowsForms: false, expectedResult: new List<(string, string)>
                {
                    ("netcoreapp3.0", ".NET Core 3.0"),
                    ("netcoreapp3.1", ".NET Core 3.1"),
                    ("net5.0-windows7.0", ".NET 5"),
                    ("net6.0-windows7.0", ".NET 6")
                });
        }

        [Theory]
        [InlineData(".NETCoreApp,Version=v5.0", "", true, false)]
        [InlineData(".NETCoreApp,Version=v5.0", "", false, true)]
        [InlineData(".NETCoreApp,Version=v5.0", "Windows,Version=7.0", true, false)]
        [InlineData(".NETCoreApp,Version=v5.0", "Windows,Version=7.0", false, true)]
        [InlineData(".NETCoreApp,Version=v3.1", "", true, false)]
        [InlineData(".NETCoreApp,Version=v3.1", "", false, true)]
        public void It_generates_supported_target_framework_alias_items_when_using_wpf_or_winforms(string targetFrameworkMoniker, string targetPlatformMoniker, bool UseWpf, bool UseWindowsForms)
        {
            RunTask(targetFrameworkMoniker, targetPlatformMoniker, UseWpf, UseWindowsForms, expectedResult: new List<(string, string)>
                {
                    ("netcoreapp3.0", ".NET Core 3.0"),
                    ("netcoreapp3.1", ".NET Core 3.1"),
                    ("net5.0-windows", ".NET 5"),
                    ("net6.0-windows", ".NET 6")
                });
        }

        private void RunTask(string targetFrameworkMoniker, string targetPlatformMoniker, bool UseWpf, bool UseWindowsForms, List<(string, string)> expectedResult)
        {
            Func<List<(string, string)>, ITaskItem[]> convertToItems = (List<(string, string)> list) => list.Select(item => new TaskItem(item.Item1, new Dictionary<string, string>() { { "DisplayName", item.Item2 } })).ToArray();

            var task = new GenerateSupportedTargetFrameworkAlias()
            {
                SupportedTargetFramework = convertToItems(MockSupportedTargetFramework),
                TargetFrameworkMoniker = targetFrameworkMoniker,
                TargetPlatformMoniker = targetPlatformMoniker,
                UseWpf = UseWpf,
                UseWindowsForms = UseWindowsForms
            };
            task.Execute();

            task.SupportedTargetFrameworkAlias.Should().BeEquivalentTo(convertToItems(expectedResult));
        }
    }
}

