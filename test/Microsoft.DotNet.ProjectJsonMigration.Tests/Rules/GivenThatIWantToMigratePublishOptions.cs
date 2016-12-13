using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.Internal.ProjectModel.Files;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigratePublishOptions : TestBase
    {
        [Fact]
        private void MigratingPublishOptionsIncludeExcludePopulatesContentItem()
        {
            var testDirectory = Temp.CreateDirectory().Path;
            WriteFilesInProjectDirectory(testDirectory);

            var mockProj = RunPublishOptionsRuleOnPj(@"
                {
                    ""publishOptions"": {
                        ""include"": [""root"", ""src"", ""rootfile.cs""],
                        ""exclude"": [""src"", ""rootfile.cs""],
                        ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                        ""excludeFiles"": [""src/file2.cs""]
                    }
                }",
                testDirectory: testDirectory);

            mockProj.Items.Count(i => i.ItemType.Equals("Content", StringComparison.Ordinal)).Should().Be(2);

            foreach (var item in mockProj.Items.Where(i => i.ItemType.Equals("Content", StringComparison.Ordinal)))
            {
                item.Metadata.Count(m => m.Name == "CopyToPublishDirectory").Should().Be(1);

                if (item.Update.Contains(@"src\file1.cs"))
                {
                    item.Update.Should().Be(@"src\file1.cs;src\file2.cs");
                    item.Exclude.Should().Be(@"src\file2.cs");
                }
                else
                {
                    item.Update.Should()
                        .Be(@"root\**\*;src\**\*;rootfile.cs");

                    item.Exclude.Should()
                        .Be(@"src\**\*;rootfile.cs;src\file2.cs");
                }
            }
        }

        [Fact]
        private void MigratingPublishOptionsAndBuildOptionsCopyToOutputDoesMergesContentItemsBecausePublishIsUpdateAndBuildIsInclude()
        {
            var testDirectory = Temp.CreateDirectory().Path;
            WriteFilesInProjectDirectory(testDirectory);

            var mockProj = RunPublishAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""copyToOutput"": {
                            ""include"": [""src"", ""rootfile.cs""],
                            ""exclude"": [""src"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""src/file2.cs""]
                        }
                    },
                    ""publishOptions"": {
                        ""include"": [""root"", ""src"", ""rootfile.cs""],
                        ""exclude"": [""src"", ""rootfile.cs""],
                        ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                        ""excludeFiles"": [""src/file3.cs""]
                    }
                }",
                testDirectory: testDirectory);

            mockProj.Items.Count(i => i.ItemType.Equals("Content", StringComparison.Ordinal)).Should().Be(4);

            // From ProjectReader #L725 (Both are empty)
            var defaultIncludePatterns = Enumerable.Empty<string>();
            var defaultExcludePatterns = ProjectFilesCollection.DefaultPublishExcludePatterns;

            foreach (var item in mockProj.Items.Where(i => i.ItemType.Equals("Content", StringComparison.Ordinal)))
            {
                if (item.Update.Contains(@"root\**\*"))
                {
                    item.Update.Should().Be(@"root\**\*;src\**\*;rootfile.cs");
                    item.Exclude.Should().Be(@"src\**\*;rootfile.cs;src\file3.cs");
                }
                else if (item.Update.Contains(@"src\file1.cs"))
                {
                    item.Update.Should().Be(@"src\file1.cs;src\file2.cs");
                    item.Exclude.Should().Be(@"src\file3.cs");
                }
                else if (item.Include.Contains(@"src\file1.cs"))
                {
                    item.Include.Should().Be(@"src\file1.cs;src\file2.cs");
                    item.Exclude.Should().Be(@"src\file2.cs");
                }
                else
                {
                    item.Include.Should()
                        .Be(@"src\**\*;rootfile.cs");

                    item.Exclude.Should()
                        .Be(@"src\**\*;rootfile.cs;src\file2.cs");
                }
            }
        }

        private void WriteFilesInProjectDirectory(string testDirectory)
        {
            Directory.CreateDirectory(Path.Combine(testDirectory, "root"));
            Directory.CreateDirectory(Path.Combine(testDirectory, "src"));
            File.WriteAllText(Path.Combine(testDirectory, "root", "file1.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "root", "file2.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "root", "file3.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file1.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file2.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file3.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "rootfile.cs"), "content");
        }

        private ProjectRootElement RunPublishOptionsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigratePublishOptionsRule()
            }, s, testDirectory);
        }

        private ProjectRootElement RunPublishAndBuildOptionsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigrateBuildOptionsRule(),
                new MigratePublishOptionsRule()
            }, s, testDirectory);
        }
    }
}