// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Microsoft.Extensions.EnvironmentAbstractions;
using Microsoft.NET.TestFramework;
using NuGet.Frameworks;
using NuGet.Versioning;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    public class LocalToolsResolverCacheTests : SdkTest
    {
        public LocalToolsResolverCacheTests(ITestOutputHelper log) : base(log)
        {
        }

        private static
            (DirectoryPath nuGetGlobalPackagesFolder,
            LocalToolsResolverCache localToolsResolverCache) Setup()
        {
            IFileSystem fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();
            DirectoryPath tempDirectory =
                new DirectoryPath(fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath);
            DirectoryPath cacheDirectory = tempDirectory.WithSubDirectories("cacheDirectory");
            DirectoryPath nuGetGlobalPackagesFolder = tempDirectory.WithSubDirectories("nugetGlobalPackageLocation");
            fileSystem.Directory.CreateDirectory(cacheDirectory.Value);
            const int version = 1;

            LocalToolsResolverCache localToolsResolverCache =
                new LocalToolsResolverCache(fileSystem, cacheDirectory, version);
            return (nuGetGlobalPackagesFolder, localToolsResolverCache);
        }

        [Fact]
        public void GivenExecutableIdentifierItCanSaveAndCannotLoadWithMismatches()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
                new RestoredCommand(new ToolCommandName("tool2"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool2.dll"))
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache
                .TryLoad(
                    new RestoredCommandIdentifier(packageId, NuGetVersion.Parse("1.0.0-wrong-version"), targetFramework,
                        runtimeIdentifier, restoredCommands[0].Name), out _)
                .Should().BeFalse();

            localToolsResolverCache
                .TryLoad(
                    new RestoredCommandIdentifier(packageId, nuGetVersion, NuGetFramework.Parse("wrongFramework"),
                        runtimeIdentifier, restoredCommands[0].Name), out _)
                .Should().BeFalse();

            localToolsResolverCache
                .TryLoad(
                    new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework,
                        "wrongRuntimeIdentifier", restoredCommands[0].Name),
                    out _)
                .Should().BeFalse();
        }

        [Fact]
        public void GivenExecutableIdentifierItCanSaveAndLoad()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
                new RestoredCommand(new ToolCommandName("tool2"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool2.dll"))
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[0].Name),
                out RestoredCommand tool1).Should().BeTrue();

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[1].Name),
                out RestoredCommand tool2).Should().BeTrue();

            tool1.ShouldBeEquivalentTo(restoredCommands[0]);
            tool2.ShouldBeEquivalentTo(restoredCommands[1]);
        }

        [Fact]
        public void GivenExecutableIdentifierItCanSaveMultipleSameAndLoadContainsOnlyOne()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
                new RestoredCommand(new ToolCommandName("tool2"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool2.dll"))
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[0].Name),
                out RestoredCommand tool1);

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[1].Name),
                out RestoredCommand tool2);


            tool1.ShouldBeEquivalentTo(restoredCommands[0]);
            tool2.ShouldBeEquivalentTo(restoredCommands[1]);
        }

        [Fact]
        public void ItCanSaveMultipleSameAndLoadTheHighestFromVersionRange()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");

            NuGetVersion previewNuGetVersion = NuGetVersion.Parse("0.0.2");
            IReadOnlyList<RestoredCommand> restoredCommandsV0 = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1preview.dll")),
            };

            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommandsV1 = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
            };

            NuGetVersion newerNuGetVersion = NuGetVersion.Parse("2.0.2");
            IReadOnlyList<RestoredCommand> restoredCommandsV2 = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1new.dll")),
            };

            localToolsResolverCache.Save(
                restoredCommandsV0.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, previewNuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.Save(
                restoredCommandsV1.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.Save(
                restoredCommandsV2.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, newerNuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            bool loadSuccess =
                localToolsResolverCache.TryLoadHighestVersion(
                    new RestoredCommandIdentifierVersionRange(
                        packageId,
                        VersionRange.Parse("(0.0.0, 2.0.0)"),
                        targetFramework, runtimeIdentifier, new ToolCommandName("tool1")),
                    out RestoredCommand loadedResolverCache);

            loadSuccess.Should().BeTrue();

            loadedResolverCache.ShouldBeEquivalentTo(restoredCommandsV1[0]);
        }

        [Fact]
        public void ItReturnsFalseWhenFailedToLoadVersionRange()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            bool loadSuccess =
                localToolsResolverCache.TryLoadHighestVersion(
                    new RestoredCommandIdentifierVersionRange(
                        new PackageId("my.toolBundle"),
                        VersionRange.Parse("(0.0.0, 2.0.0)"),
                        NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework), Constants.AnyRid, new ToolCommandName("tool1")),
                    out _);

            loadSuccess.Should().BeFalse();
        }

        [Fact]
        public void GivenExecutableIdentifierItCanSaveMultipleVersionAndLoad()
        {
            (DirectoryPath nuGetGlobalPackagesFolder, LocalToolsResolverCache localToolsResolverCache) = Setup();

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
            };

            NuGetVersion newerNuGetVersion = NuGetVersion.Parse("2.0.2");
            IReadOnlyList<RestoredCommand> restoredCommandsNewer = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1new.dll")),
                new RestoredCommand(new ToolCommandName("tool2"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool2new.dll")),
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.Save(
                restoredCommandsNewer.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, newerNuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[0].Name),
                out RestoredCommand tool1);
            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, newerNuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommandsNewer[0].Name),
                out RestoredCommand tool1Newer);

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, newerNuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommandsNewer[1].Name),
                out RestoredCommand tool2Newer);

            tool1.ShouldBeEquivalentTo(restoredCommands[0]);
            tool1Newer.ShouldBeEquivalentTo(restoredCommandsNewer[0]);
            tool2Newer.ShouldBeEquivalentTo(restoredCommandsNewer[1]);
        }

        [Fact]
        public void WhenTheCacheIsCorruptedByAppendingLineItShouldLoadAsEmpty()
        {
            WhenTheCacheIsCorruptedItShouldLoadAsEmpty(
                useRealFileSystem: false,
                corruptCache: (fileSystem, cachePath, existingCache) =>
                    fileSystem.File.WriteAllText(cachePath, existingCache + " !!!Corrupted")
            );
        }

        [Fact]
        public void WhenTheCacheIsCorruptedByNotAJsonItShouldLoadAsEmpty()
        {
            WhenTheCacheIsCorruptedItShouldLoadAsEmpty(
                useRealFileSystem: true,
                corruptCache: (fileSystem, cachePath, existingCache) =>
                {
                    File.WriteAllBytes(cachePath, new byte[] {0x12, 0x23, 0x34, 0x45});
                }
            );
        }

        [Fact]
        public void WhenTheCacheIsCorruptedItShouldNotAffectNextSaveAndLoad()
        {
            IFileSystem fileSystem = new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build();

            DirectoryPath tempDirectory =
                new DirectoryPath(fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath);
            DirectoryPath cacheDirectory = tempDirectory.WithSubDirectories("cacheDirectory");
            DirectoryPath nuGetGlobalPackagesFolder = tempDirectory.WithSubDirectories("nugetGlobalPackageLocation");
            fileSystem.Directory.CreateDirectory(cacheDirectory.Value);
            const int version = 1;

            LocalToolsResolverCache localToolsResolverCache =
                new LocalToolsResolverCache(fileSystem, cacheDirectory, version);

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            var cachePath = cacheDirectory
                .WithSubDirectories(version.ToString())
                .WithSubDirectories(packageId.ToString()).Value;
            var existingCache =
                fileSystem.File.ReadAllText(
                    cachePath);
            existingCache.Should().NotBeEmpty();

            fileSystem.File.WriteAllText(cachePath, existingCache + " !!!Corrupted");

            // Save after corruption
            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[0].Name),
                out RestoredCommand restoredCommand);

            restoredCommand.ShouldBeEquivalentTo(restoredCommands[0]);
        }

        private static void WhenTheCacheIsCorruptedItShouldLoadAsEmpty(
            bool useRealFileSystem,
            Action<IFileSystem, string, string> corruptCache)
        {
            IFileSystem fileSystem =
                useRealFileSystem == false
                    ? new FileSystemMockBuilder().UseCurrentSystemTemporaryDirectory().Build()
                    : new FileSystemWrapper();

            DirectoryPath tempDirectory =
                new DirectoryPath(fileSystem.Directory.CreateTemporaryDirectory().DirectoryPath);
            DirectoryPath cacheDirectory = tempDirectory.WithSubDirectories("cacheDirectory");
            DirectoryPath nuGetGlobalPackagesFolder = tempDirectory.WithSubDirectories("nugetGlobalPackageLocation");
            fileSystem.Directory.CreateDirectory(cacheDirectory.Value);
            const int version = 1;

            LocalToolsResolverCache localToolsResolverCache =
                new LocalToolsResolverCache(fileSystem, cacheDirectory, version);

            NuGetFramework targetFramework = NuGetFramework.Parse(ToolsetInfo.CurrentTargetFramework);
            string runtimeIdentifier = Constants.AnyRid;
            PackageId packageId = new PackageId("my.toolBundle");
            NuGetVersion nuGetVersion = NuGetVersion.Parse("1.0.2");
            IReadOnlyList<RestoredCommand> restoredCommands = new[]
            {
                new RestoredCommand(new ToolCommandName("tool1"), "dotnet", nuGetGlobalPackagesFolder.WithFile("tool1.dll")),
            };

            localToolsResolverCache.Save(
                restoredCommands.ToDictionary(
                    c => new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                        c.Name)));

            var cachePath = cacheDirectory
                .WithSubDirectories(version.ToString())
                .WithSubDirectories(packageId.ToString()).Value;
            var existingCache =
                fileSystem.File.ReadAllText(
                    cachePath);
            existingCache.Should().NotBeEmpty();

            corruptCache(fileSystem, cachePath, existingCache);

            localToolsResolverCache.TryLoad(
                new RestoredCommandIdentifier(packageId, nuGetVersion, targetFramework, runtimeIdentifier,
                    restoredCommands[0].Name),
                out _).Should().BeFalse("Consider corrupted file cache miss");
        }
    }
}
