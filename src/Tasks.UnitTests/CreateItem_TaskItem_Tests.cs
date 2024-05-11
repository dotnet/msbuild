// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class CreateItem_TaskItem_Tests
    {
        private const string ProjectUsingTaskItemGroup =
            """
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`TargetUsingItemGroup`>
                        <ItemGroup>
                            <Items Include=`{0}` />
                        </ItemGroup>
                    </Target>
                </Project>
            """;

        private const string ProjectUsingTaskItemGroupWithFixFilePathFalse =
            """
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`TargetUsingItemGroup`>
                        <ItemGroup>
                            <Items Include=`{0}`>
                                <FixFilePath>false</FixFilePath>
                            </Items>
                        </ItemGroup>
                    </Target>
                </Project>
            """;

        private const string ProjectUsingTaskItemGroupWithTargetOs =
            """
                <Project ToolsVersion=`msbuilddefaulttoolsversion`>
                    <Target Name=`TargetUsingItemGroup`>
                        <ItemGroup>
                            <Items Include=`{0}`>
                                <TargetOs>{1}</TargetOs>
                            </Items>
                        </ItemGroup>
                    </Target>
                </Project>
            """;

        /// <summary>
        /// CreateItem automatically fixes the directory separator in the Include items by default
        /// (this is the current behaviour, and we cannot change that without a large impact)
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashes))]
        public void FixesDirectorySeparatorCharByDefault(string original, string expected)
        {
            CreateItem t = new() { BuildEngine = new MockEngine(), Include = [new TaskItem(original)], };

            bool success = t.Execute();
            success.ShouldBeTrue();

            t.Include[0].ItemSpec.ShouldBe(expected);
        }

        /// <summary>
        /// CreateItem automatically fixes the directory separator in the Include items by default
        /// (this is the current behaviour, and we cannot change that without a large impact)
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashes))]
        public void FixesDirectorySeparatorCharByDefaultInAnActualProject(string original, string expected)
        {
            string projectFile = RandomProjectFile();
            string projectContent = string.Format(ProjectUsingTaskItemGroup, original);

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(
                ObjectModelHelpers.CreateFileInTempProjectDirectory(projectFile,
                    projectContent));

            var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
            Assert.True(instance.Build(["TargetUsingItemGroup"], []));

            var items = instance.GetItems("Items");
            items.ShouldHaveSingleItem();
            items.First().EvaluatedInclude.ShouldBe(expected);
        }

        /// <summary>
        /// CreateItem does not automatically fix the directory separator in the Include item if the
        /// special metadata item FixFilePath is set to false
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashes))]
        public void DoesNotFixDirectorySeparatorCharIfSpecialMetaDataIsSet(string original, string _)
        {
            var metadata = new Dictionary<string, string> { { "FixFilePath", "false" }, };

            CreateItem t = new() { BuildEngine = new MockEngine(), Include = [new TaskItem(original, metadata)], };

            bool success = t.Execute();
            success.ShouldBeTrue();

            t.Include[0].ItemSpec.ShouldBe(original);
        }

        /// <summary>
        /// CreateItem does not automatically fix the directory separator in the Include item if the
        /// special metadata item FixFilePath is set to false
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashes))]
        public void DoesNotFixDirectorySeparatorCharIfSpecialMetaDataIsSetInAnActualProject(string original, string _)
        {
            string projectFile = RandomProjectFile();
            string projectContent = string.Format(ProjectUsingTaskItemGroupWithFixFilePathFalse, original);

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(
                ObjectModelHelpers.CreateFileInTempProjectDirectory(projectFile,
                    projectContent));

            var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
            Assert.True(instance.Build(["TargetUsingItemGroup"], []));

            var items = instance.GetItems("Items");
            items.ShouldHaveSingleItem();
            items.First().EvaluatedInclude.ShouldBe(original);
        }

        /// <summary>
        /// CreateItem uses the target platform when fixing the directory separator if the
        /// special metadata item TargetPlatform is set
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashesAndTargetOs))]
        public void FixesDirectorySeparatorCharToSuppliedTargetPlatform(string platform, string original, string expected)
        {
            var metadata = new Dictionary<string, string> { { "TargetOs", platform }, };

            CreateItem t = new() { BuildEngine = new MockEngine(), Include = [new TaskItem(original, metadata)], };

            bool success = t.Execute();
            success.ShouldBeTrue();

            t.Include[0].ItemSpec.ShouldBe(expected);
        }

        /// <summary>
        /// CreateItem uses the target platform when fixing the directory separator if the
        /// special metadata item TargetPlatform is set
        /// </summary>
        [Theory]
        [MemberData(nameof(PathsWithVariousSlashesAndTargetOs))]
        public void FixesDirectorySeparatorCharToSuppliedTargetPlatformInAnActualProject(string targetOs, string original, string expected)
        {
            string projectFile = RandomProjectFile();
            string projectContent = string.Format(ProjectUsingTaskItemGroupWithTargetOs, original, targetOs);

            Project project = ObjectModelHelpers.LoadProjectFileInTempProjectDirectory(
                ObjectModelHelpers.CreateFileInTempProjectDirectory(projectFile,
                    projectContent));

            var instance = project.CreateProjectInstance(ProjectInstanceSettings.None);
            Assert.True(instance.Build(["TargetUsingItemGroup"], []));

            var items = instance.GetItems("Items");
            items.ShouldHaveSingleItem();
            items.First().EvaluatedInclude.ShouldBe(expected);
        }


        public static TheoryData<string, string> PathsWithVariousSlashes
        {
            get
            {
                char s = Path.DirectorySeparatorChar;
                return new TheoryData<string, string>
                {
                    { @"C:\windows\path\anyfile.txt", $"C:{s}windows{s}path{s}anyfile.txt" },
                    { @"unrooted\windows\path\anyfile.txt", $"unrooted{s}windows{s}path{s}anyfile.txt" },
                    { @"C:/windows/path/with/unix/slashes/anyfile.txt", $"C:{s}windows{s}path{s}with{s}unix{s}slashes{s}anyfile.txt" },
                    { @"/unixpath/anyfile.txt", $"{s}unixpath{s}anyfile.txt" },
                    { @"/mixed\paths/anyfile.txt", $"{s}mixed{s}paths{s}anyfile.txt" },
                };
            }
        }

        public static TheoryData<string, string, string> PathsWithVariousSlashesAndTargetOs
        {
            get
            {
                char s = Path.DirectorySeparatorChar;
                char w = '\\';
                char u = '/';
                return new TheoryData<string, string, string>
                {
                    { "windows", @"C:\windows\path\anyfile.txt", $"C:{w}windows{w}path{w}anyfile.txt" },
                    { "windows", @"unrooted\windows\path\anyfile.txt", $"unrooted{w}windows{w}path{w}anyfile.txt" },
                    { "windows", @"C:/windows/path/with/unix/slashes/anyfile.txt", $"C:{w}windows{w}path{w}with{w}unix{w}slashes{w}anyfile.txt" },
                    { "windows", @"/unixpath/anyfile.txt", $"{w}unixpath{w}anyfile.txt" },
                    { "windows", @"/mixed\paths/anyfile.txt", $"{w}mixed{w}paths{w}anyfile.txt" },
                    { "unix", @"C:\windows\path\anyfile.txt", $"C:{u}windows{u}path{u}anyfile.txt" },
                    { "unix", @"unrooted\windows\path\anyfile.txt", $"unrooted{u}windows{u}path{u}anyfile.txt" },
                    { "unix", @"C:/windows/path/with/unix/slashes/anyfile.txt", $"C:{u}windows{u}path{u}with{u}unix{u}slashes{u}anyfile.txt" },
                    { "unix", @"/unixpath/anyfile.txt", $"{u}unixpath{u}anyfile.txt" },
                    { "unix", @"/mixed\paths/anyfile.txt", $"{u}mixed{u}paths{u}anyfile.txt" },
                    { "current", @"C:\windows\path\anyfile.txt", $"C:{s}windows{s}path{s}anyfile.txt" },
                    { "current", @"unrooted\windows\path\anyfile.txt", $"unrooted{s}windows{s}path{s}anyfile.txt" },
                    { "current", @"C:/windows/path/with/current/slashes/anyfile.txt", $"C:{s}windows{s}path{s}with{s}current{s}slashes{s}anyfile.txt" },
                    { "current", @"/currentpath/anyfile.txt", $"{s}currentpath{s}anyfile.txt" },
                    { "current", @"/mixed\paths/anyfile.txt", $"{s}mixed{s}paths{s}anyfile.txt" },
                    { null, @"C:\windows\path\anyfile.txt", $"C:{s}windows{s}path{s}anyfile.txt" },
                    { "_anything", @"unrooted\windows\path\anyfile.txt", $"unrooted{s}windows{s}path{s}anyfile.txt" },
                    { "_not_valid", @"C:/windows/path/with/current/slashes/anyfile.txt", $"C:{s}windows{s}path{s}with{s}current{s}slashes{s}anyfile.txt" },
                    { "_invalid", @"/currentpath/anyfile.txt", $"{s}currentpath{s}anyfile.txt" },
                    { "_run_with_default", @"/mixed\paths/anyfile.txt", $"{s}mixed{s}paths{s}anyfile.txt" },
                };
            }
        }

        private static string RandomProjectFile() =>
            Random.Shared.GetItems("abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray(), 8)
                .ToString();
    }
}
