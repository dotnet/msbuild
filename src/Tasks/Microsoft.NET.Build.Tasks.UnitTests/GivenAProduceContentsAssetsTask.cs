// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAProduceContentsAssetsTask
    {
        [Fact]
        public void ItProcessesContentFiles()
        {
            // sample data
            string sampleppTxt = Path.Combine("contentFiles", "any", "samplepp.txt");
            string inputText = "This is the $rootnamespace$ of $filename$";
            string rootNamespace = "LibA.Test";
            string fileName = "B.cs";
            var preprocessorValues = new Dictionary<string, string>()
            {
                { "rootnamespace", rootNamespace },
                { "filename", fileName },
            };

            // mock preprocessor
            var assetPreprocessor = new MockContentAssetPreprocessor((s) => false);
            assetPreprocessor.MockReadContent = inputText;

            // input items
            var contentPreprocessorValues = GetPreprocessorValueItems(preprocessorValues);
            var contentFileDefinitions = new ITaskItem[]
            {
                GetFileDef("LibA/1.2.3", sampleppTxt)
            };
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep("LibA/1.2.3", sampleppTxt, ppOutputPath: "samplepp.output.txt")
            };

            // execute task
            var task = new ProduceContentAssets(assetPreprocessor)
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = contentPreprocessorValues,
                ContentPreprocessorOutputDirectory = ContentOutputDirectory,
                ProjectLanguage = null,
            };
            task.Execute().Should().BeTrue();

            // Asserts
            assetPreprocessor.MockWrittenContent.Should().Be($"This is the {rootNamespace} of {fileName}");
            task.FileWrites.Count().Should().Be(1);
            task.FileWrites.Select(t => t.ItemSpec)
                .Should().Contain(Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp.output.txt"));
            task.CopyLocalItems.Count().Should().Be(0); // copyToOutput = false
            task.ProcessedContentItems.Count().Should().Be(0); // buildAction = none
        }

        [Fact]
        public void ItOutputsFileWritesForProcessedContent()
        {
            // sample data
            string [] contentFiles = new string[] 
            {
                Path.Combine("contentFiles", "any", "samplepp.txt"),
                Path.Combine("contentFiles", "any", "image.png"),
                Path.Combine("contentFiles", "any", "plain.txt"),
            };
            string inputText = "This is the $rootnamespace$ of $filename$";
            string rootNamespace = "LibA.Test";
            string fileName = "B.cs";
            var preprocessorValues = new Dictionary<string, string>()
            {
                { "rootnamespace", rootNamespace },
                { "filename", fileName },
            };

            // mock preprocessor
            var assetPreprocessor = new MockContentAssetPreprocessor((s) => false);
            assetPreprocessor.MockReadContent = inputText;

            // input items
            var contentPreprocessorValues = GetPreprocessorValueItems(preprocessorValues);
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef("LibA/1.2.3", c)).ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep("LibA/1.2.3", contentFiles[0], buildAction: "Content", ppOutputPath: "samplepp.output.txt"),
                GetFileDep("LibA/1.2.3", contentFiles[1], buildAction: "EmbeddedResource"),
                GetFileDep("LibA/1.2.3", contentFiles[2], buildAction: "Content"),
            };

            // execute task
            var task = new ProduceContentAssets(assetPreprocessor)
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = contentPreprocessorValues,
                ContentPreprocessorOutputDirectory = ContentOutputDirectory,
                ProjectLanguage = null,
            };
            task.Execute().Should().BeTrue();

            // Asserts
            assetPreprocessor.MockWrittenContent.Should().Be($"This is the {rootNamespace} of {fileName}");
            task.FileWrites.Count().Should().Be(1);
            task.FileWrites.Select(t => t.ItemSpec)
                .Should().Contain(Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp.output.txt"));
            task.ProcessedContentItems.Count().Should().Be(3);
            task.CopyLocalItems.Count().Should().Be(0);
        }

        [Fact]
        public void ItOutputsCopyLocalItems()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "any", "samplepp.txt"),
                Path.Combine("contentFiles", "any", "image.png"),
                Path.Combine("contentFiles", "any", "plain.txt"),
                Path.Combine("donotcopy", "README.md"),
            };
            string inputText = "This is the $rootnamespace$ of $filename$";
            string rootNamespace = "LibA.Test";
            string fileName = "B.cs";
            var preprocessorValues = new Dictionary<string, string>()
            {
                { "rootnamespace", rootNamespace },
                { "filename", fileName },
            };

            // mock preprocessor
            var assetPreprocessor = new MockContentAssetPreprocessor((s) => false);
            assetPreprocessor.MockReadContent = inputText;

            // input items
            var contentPreprocessorValues = GetPreprocessorValueItems(preprocessorValues);
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c)).ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep(package, contentFiles[0], copyToOutput: true, ppOutputPath: "samplepp.output.txt"),
                GetFileDep(package, contentFiles[1], copyToOutput: true, 
                    outputPath: Path.Combine("output", contentFiles[1])),
                GetFileDep(package, contentFiles[2], copyToOutput: true, 
                    outputPath: Path.Combine("output", contentFiles[2])),
                GetFileDep(package, contentFiles[3], copyToOutput: false),
            };

            // execute task
            var task = new ProduceContentAssets(assetPreprocessor)
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = contentPreprocessorValues,
                ContentPreprocessorOutputDirectory = ContentOutputDirectory,
                ProjectLanguage = null,
            };
            task.Execute().Should().BeTrue();

            // Asserts
            assetPreprocessor.MockWrittenContent.Should().Be($"This is the {rootNamespace} of {fileName}");
            string assetWritePath = Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp.output.txt");

            task.FileWrites.Count().Should().Be(1);
            task.FileWrites.Select(t => t.ItemSpec).Should().Contain(assetWritePath);
            task.ProcessedContentItems.Count().Should().Be(0);

            var copyLocalItems = task.CopyLocalItems;
            copyLocalItems.Count().Should().Be(3);

            var item = copyLocalItems.Where(t => t.ItemSpec.EndsWith(assetWritePath)).First();
            item.GetMetadata("TargetPath").Should().Be("samplepp.output.txt");
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);
            item.GetMetadata(MetadataKeys.PackageName).Should().Be("LibA");
            item.GetMetadata(MetadataKeys.PackageVersion).Should().Be("1.2.3");

            for (int i = 1; i < 3; i++)
            {
                item = copyLocalItems.Where(t => t.ItemSpec.EndsWith(contentFiles[i])).First();
                item.GetMetadata("TargetPath").Should().Be(Path.Combine("output", contentFiles[i]));
                item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);
                item.GetMetadata(MetadataKeys.PackageName).Should().Be("LibA");
                item.GetMetadata(MetadataKeys.PackageVersion).Should().Be("1.2.3");
            }

            // not added to copy
            copyLocalItems.Where(t => t.ItemSpec.EndsWith(contentFiles[3])).Should().BeEmpty();
        }

        [Fact]
        public void ItOutputsContentItemsWithActiveBuildAction()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "any", "samplepp.txt"),
                Path.Combine("contentFiles", "any", "image.png"),
                Path.Combine("contentFiles", "any", "plain.txt"),
                Path.Combine("donotcopy", "README.md"),
            };
            string inputText = "This is the $rootnamespace$ of $filename$";
            string rootNamespace = "LibA.Test";
            string fileName = "B.cs";
            var preprocessorValues = new Dictionary<string, string>()
            {
                { "rootnamespace", rootNamespace },
                { "filename", fileName },
            };

            // mock preprocessor
            var assetPreprocessor = new MockContentAssetPreprocessor((s) => false);
            assetPreprocessor.MockReadContent = inputText;

            // input items
            var contentPreprocessorValues = GetPreprocessorValueItems(preprocessorValues);
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c)).ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep(package, contentFiles[0], buildAction: "Content", ppOutputPath: "samplepp.output.txt"),
                GetFileDep(package, contentFiles[1], buildAction: "EmbeddedResource"),
                GetFileDep(package, contentFiles[2], buildAction: "Content"),
                GetFileDep(package, contentFiles[3], buildAction: "None"),
            };

            // execute task
            var task = new ProduceContentAssets(assetPreprocessor)
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = contentPreprocessorValues,
                ContentPreprocessorOutputDirectory = ContentOutputDirectory,
                ProjectLanguage = null,
            };
            task.Execute().Should().BeTrue();

            // Asserts
            assetPreprocessor.MockWrittenContent.Should().Be($"This is the {rootNamespace} of {fileName}");
            string assetWritePath = Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp.output.txt");

            task.FileWrites.Count().Should().Be(1);
            task.FileWrites.Select(t => t.ItemSpec).Should().Contain(assetWritePath);
            task.CopyLocalItems.Count().Should().Be(0);

            var contentItems = task.ProcessedContentItems;
            contentItems.Count().Should().Be(3);

            var item = contentItems.Where(t => t.ItemSpec.EndsWith(assetWritePath)).First();
            item.GetMetadata("ProcessedItemType").Should().Be("Content");
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);

            item = contentItems.Where(t => t.ItemSpec.EndsWith(contentFiles[1])).First();
            item.GetMetadata("ProcessedItemType").Should().Be("EmbeddedResource");
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);

            item = contentItems.Where(t => t.ItemSpec.EndsWith(contentFiles[2])).First();
            item.GetMetadata("ProcessedItemType").Should().Be("Content");
            item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);

            // not added to content items
            contentItems.Where(t => t.ItemSpec.EndsWith(contentFiles[3])).Should().BeEmpty();
        }

        [Fact]
        public void ItCanOutputOnlyPreprocessedItems()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "any", "samplepp1.txt"),
                Path.Combine("contentFiles", "any", "samplepp2.pp"),
                Path.Combine("contentFiles", "any", "image.png"),
                Path.Combine("contentFiles", "any", "plain.txt"),
            };
            string inputText = "This is the $rootnamespace$ of $filename$";
            string rootNamespace = "LibA.Test";
            string fileName = "B.cs";
            var preprocessorValues = new Dictionary<string, string>()
            {
                { "rootnamespace", rootNamespace },
                { "filename", fileName },
            };

            // mock preprocessor
            var assetPreprocessor = new MockContentAssetPreprocessor((s) => false);
            assetPreprocessor.MockReadContent = inputText;

            // input items
            var contentPreprocessorValues = GetPreprocessorValueItems(preprocessorValues);
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c)).ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep(package, contentFiles[0], buildAction: "Content", copyToOutput: true, 
                    ppOutputPath: "samplepp1.output.txt"),
                GetFileDep(package, contentFiles[0], buildAction: "Content", copyToOutput: true,
                    ppOutputPath: "samplepp2.output.txt"),
                GetFileDep(package, contentFiles[2], buildAction: "Content", copyToOutput: true,
                    outputPath: Path.Combine("output", contentFiles[2])),
                GetFileDep(package, contentFiles[3], buildAction: "Content", copyToOutput: true,
                    outputPath: Path.Combine("output", contentFiles[3])),
            };

            // execute task
            var task = new ProduceContentAssets(assetPreprocessor)
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = contentPreprocessorValues,
                ContentPreprocessorOutputDirectory = ContentOutputDirectory,
                ProduceOnlyPreprocessorFiles = true,
                ProjectLanguage = null,
            };
            task.Execute().Should().BeTrue();

            // Asserts
            string[] assetWritePaths = new string[] 
            {
                Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp1.output.txt"),
                Path.Combine(ContentOutputDirectory, "test", "LibA", "1.2.3", "samplepp2.output.txt")
            };

            task.FileWrites.Count().Should().Be(2);
            task.FileWrites.Select(t => t.ItemSpec).Should().Contain(assetWritePaths);

            // only 2 of 4 content files should be processed
            var processedContentItems = task.ProcessedContentItems;
            processedContentItems.Count().Should().Be(2);
            processedContentItems.Where(t => t.ItemSpec.EndsWith(assetWritePaths[0])).Should().NotBeEmpty();
            processedContentItems.Where(t => t.ItemSpec.EndsWith(assetWritePaths[1])).Should().NotBeEmpty();
            processedContentItems.Where(t => t.ItemSpec.EndsWith(contentFiles[2])).Should().BeEmpty();
            processedContentItems.Where(t => t.ItemSpec.EndsWith(contentFiles[3])).Should().BeEmpty();

            // only 2 of 4 content files should be copied
            var copyLocalItems = task.CopyLocalItems;
            copyLocalItems.Count().Should().Be(2);
            copyLocalItems.Where(t => t.ItemSpec.EndsWith(assetWritePaths[0])).Should().NotBeEmpty();
            copyLocalItems.Where(t => t.ItemSpec.EndsWith(assetWritePaths[1])).Should().NotBeEmpty();
            copyLocalItems.Where(t => t.ItemSpec.EndsWith(contentFiles[2])).Should().BeEmpty();
            copyLocalItems.Where(t => t.ItemSpec.EndsWith(contentFiles[3])).Should().BeEmpty();
        }

        [Fact]
        public void ItIgnoresProjectLanguageIfCodeLanguageIsOnlyAny()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "any", "file1.md"),
                Path.Combine("contentFiles", "any", "file2.md"),
                Path.Combine("contentFiles", "any", "file3.md"),
            };

            // input items
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c))
                .ToArray();
            var contentFileDependencies = contentFiles
                .Select(c => GetFileDep(package, c, buildAction: "Content", codeLanguage: "any"))
                .ToArray();

            // execute task
            var task = new ProduceContentAssets()
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = null,
                ContentPreprocessorOutputDirectory = null,
                ProjectLanguage = "C#",
            };
            task.Execute().Should().BeTrue();

            // Asserts
            task.FileWrites.Count().Should().Be(0);
            task.CopyLocalItems.Count().Should().Be(0);

            var contentItems = task.ProcessedContentItems;
            contentItems.Count().Should().Be(3);
            contentItems.All(t => t.GetMetadata("ProcessedItemType") == "Content").Should().BeTrue();
            contentItems.All(t => t.GetMetadata(MetadataKeys.ParentPackage) == package).Should().BeTrue();
        }

        [Fact]
        public void ItProcessesOnlyProjectLanguageIfPresent()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "cs", "file1.md"),
                Path.Combine("contentFiles", "any", "file2.md"),
                Path.Combine("contentFiles", "any", "file3.md"),
            };

            // input items
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c))
                .ToArray();
            var contentFileDependencies2 = contentFiles
                .Select(c => GetFileDep(package, c, buildAction: "Content", codeLanguage: "any"))
                .ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep(package, contentFiles[0], buildAction: "Content", codeLanguage: "cs"),
                GetFileDep(package, contentFiles[1], buildAction: "Content", codeLanguage: "any"),
                GetFileDep(package, contentFiles[2], buildAction: "Content", codeLanguage: "any"),
            };

            // execute task
            var task = new ProduceContentAssets()
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = null,
                ContentPreprocessorOutputDirectory = null,
                ProjectLanguage = "C#",
            };
            task.Execute().Should().BeTrue();

            // Asserts
            task.FileWrites.Count().Should().Be(0);
            task.CopyLocalItems.Count().Should().Be(0);

            var contentItems = task.ProcessedContentItems;
            contentItems.Count().Should().Be(1);
            contentItems.First().ItemSpec
                .Should().Be(Path.Combine(PackageRootDirectory, "LibA", "1.2.3", contentFiles[0]));
            contentItems.First().GetMetadata("ProcessedItemType").Should().Be("Content");
            contentItems.First().GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);
        }

        [Fact]
        public void ItProcessesOnlyAnyItemsIfProjectLanguageNotPresent()
        {
            // sample data
            string package = "LibA/1.2.3";
            string[] contentFiles = new string[]
            {
                Path.Combine("contentFiles", "vb", "file1.md"),
                Path.Combine("contentFiles", "any", "file2.md"),
                Path.Combine("contentFiles", "any", "file3.md"),
            };

            // input items
            var contentFileDefinitions = contentFiles
                .Select(c => GetFileDef(package, c))
                .ToArray();
            var contentFileDependencies2 = contentFiles
                .Select(c => GetFileDep(package, c, buildAction: "Content", codeLanguage: "any"))
                .ToArray();
            var contentFileDependencies = new ITaskItem[]
            {
                GetFileDep(package, contentFiles[0], buildAction: "Content", codeLanguage: "vb"),
                GetFileDep(package, contentFiles[1], buildAction: "Content", codeLanguage: "any"),
                GetFileDep(package, contentFiles[2], buildAction: "Content", codeLanguage: "any"),
            };

            // execute task
            var task = new ProduceContentAssets()
            {
                ContentFileDefinitions = contentFileDefinitions,
                ContentFileDependencies = contentFileDependencies,
                ContentPreprocessorValues = null,
                ContentPreprocessorOutputDirectory = null,
                ProjectLanguage = "C#",
            };
            task.Execute().Should().BeTrue();

            // Asserts
            task.FileWrites.Count().Should().Be(0);
            task.CopyLocalItems.Count().Should().Be(0);

            var contentItems = task.ProcessedContentItems.ToArray();
            contentItems.Count().Should().Be(2);

            ITaskItem item;
            for (int i = 0; i < 2; i++)
            {
                item = contentItems[i];
                item.ItemSpec.Should().Be(
                    Path.Combine(PackageRootDirectory, "LibA", "1.2.3", contentFiles[i+1]));
                item.GetMetadata("ProcessedItemType").Should().Be("Content");
                item.GetMetadata(MetadataKeys.ParentPackage).Should().Be(package);
            }
        }

        #region Sample Test Data

        private static readonly string ContentOutputDirectory = Path.Combine("bin", "obj");
        private static readonly string PackageRootDirectory = Path.Combine("root", "packages");

        private static ITaskItem[] GetPreprocessorValueItems(Dictionary<string, string> values)
            => values.Select(kvp => new MockTaskItem(
                itemSpec: kvp.Key,
                metadata: new Dictionary<string, string> { { "Value", kvp.Value } })).ToArray();

        private static ITaskItem GetFileDef(string package, string path)
            => new MockTaskItem(
                itemSpec: $"{package}/{path}",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.Path, path },
                    { MetadataKeys.ResolvedPath, Path.Combine(PackageRootDirectory, package.Replace('/', Path.DirectorySeparatorChar), path) },
                    { MetadataKeys.Type, "content" },
                });

        private static ITaskItem GetFileDep(string package, string path, string ppOutputPath = null,
            string codeLanguage = "any", bool copyToOutput = false, string buildAction = "none", 
            string outputPath = null)
            => new MockTaskItem(
                itemSpec: $"{package}/{path}",
                metadata: new Dictionary<string, string>
                {
                    { MetadataKeys.ParentPackage, package },
                    { MetadataKeys.ParentTarget, ".NETCoreApp,Version=v1.0" },
                    { "ppOutputPath", ppOutputPath },
                    { "codeLanguage", codeLanguage },
                    { "copyToOutput", copyToOutput.ToString() },
                    { "buildAction", buildAction },
                    { "outputPath", outputPath },
                });

        #endregion
    }
}