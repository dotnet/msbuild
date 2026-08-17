// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Tasks.UnitTests
{
    internal enum FactoryType
    {
        CodeTaskFactory,
        RoslynCodeTaskFactory,
    }

    internal static class CodeTaskFactoryEmbeddedFileInBinlogTestHelper
    {
        internal static void BuildFromSourceAndCheckForEmbeddedFileInBinlog(
            FactoryType factoryType,
            string taskName,
            string sourceContent,
            bool buildShouldSucceed)
        {
            using var env = TestEnvironment.Create();

            TransientTestFolder folder = env.CreateFolder(createFolder: true);

            var sourceClass = env.CreateFile(folder, $"{taskName}.cs", sourceContent);

            string projectFileContents = $"""
                <Project>

                  <UsingTask
                    TaskName="{taskName}"
                    TaskFactory="{factoryType}"
                    AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
                    <Task>
                      <Code Type="Class" Language="cs" Source="{sourceClass.Path}">
                      </Code>
                    </Task>
                  </UsingTask>

                    <Target Name="SayHello">
                        <{taskName} />
                    </Target>

                </Project>
                """;

            TransientTestFile binlog = env.ExpectFile(".binlog");

            var binaryLogger = new BinaryLogger()
            {
                Parameters = $"LogFile={binlog.Path}",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            };

            Helpers.BuildProjectWithNewOMAndBinaryLogger(projectFileContents, binaryLogger, out bool result, out string projectDirectoryPath);

            Assert.Equal(buildShouldSucceed, result);

            string projectImportsZipPath = Path.ChangeExtension(binlog.Path, ".ProjectImports.zip");
            using var fileStream = new FileStream(projectImportsZipPath, FileMode.Open);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // A path like "C:\path" in ZipArchive is saved as "C\path"
            // For unix-based systems path uses '/'
            projectDirectoryPath = NativeMethodsShared.IsWindows ? projectDirectoryPath.Replace(":\\", "\\") : projectDirectoryPath.Replace("/", "\\");

            // Can't just compare `Name` because `ZipArchive` does not handle unix directory separators well
            // thus producing garbled fully qualified paths in the actual .ProjectImports.zip entries
            zipArchive.Entries.ShouldContain(
                zE => zE.FullName.StartsWith(projectDirectoryPath) && zE.Name.EndsWith($"{taskName}-compilation-file.tmp"),
                $"Binlog's embedded files didn't have the expected '{projectDirectoryPath}/{{guid}}-{taskName}-compilation-file.tmp'.");
        }

        internal static void BuildAndCheckForEmbeddedFileInBinlog(
            FactoryType factoryType,
            string taskName,
            string taskXml,
            bool buildShouldSucceed)
        {
            string projectFileContents = $"""
                <Project>

                  <UsingTask
                    TaskName="{taskName}"
                    TaskFactory="{factoryType}"
                    AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
                    {taskXml}
                  </UsingTask>

                    <Target Name="SayHello">
                        <{taskName} />
                    </Target>

                </Project>
                """;

            using var env = TestEnvironment.Create();

            TransientTestFile binlog = env.ExpectFile(".binlog");

            var binaryLogger = new BinaryLogger()
            {
                Parameters = $"LogFile={binlog.Path}",
                CollectProjectImports = BinaryLogger.ProjectImportsCollectionMode.ZipFile,
            };

            Helpers.BuildProjectWithNewOMAndBinaryLogger(projectFileContents, binaryLogger, out bool result, out string projectDirectory);

            Assert.Equal(buildShouldSucceed, result);

            string projectImportsZipPath = Path.ChangeExtension(binlog.Path, ".ProjectImports.zip");
            using var fileStream = new FileStream(projectImportsZipPath, FileMode.Open);
            using var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // A path like "C:\path" in ZipArchive is saved as "C\path"
            // For unix-based systems path uses '/'
            projectDirectory = NativeMethodsShared.IsWindows ? projectDirectory.Replace(":\\", "\\") : projectDirectory.Replace("/", "\\");

            // Can't just compare `Name` because `ZipArchive` does not handle unix directory separators well
            // thus producing garbled fully qualified paths in the actual .ProjectImports.zip entries
            zipArchive.Entries.ShouldContain(
                zE => zE.FullName.StartsWith(projectDirectory) && zE.Name.EndsWith($"{taskName}-compilation-file.tmp"),
                $"Binlog's embedded files didn't have the expected '{projectDirectory}/{{guid}}-{taskName}-compilation-file.tmp'.");
        }
    }
}
