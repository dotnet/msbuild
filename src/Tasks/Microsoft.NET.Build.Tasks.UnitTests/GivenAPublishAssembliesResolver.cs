// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    /// <summary>
    /// Tests that PublishAssembliesResolver resolves assemblies correctly.
    /// </summary>
    public class GivenAPublishAssembliesResolver
    {
        [Theory]
        [MemberData("ProjectData")]
        public void ItResolvesAssembliesFromProjectLockFiles(string projectName, string runtime, object[] expectedResolvedFiles)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(projectName);
            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                runtime,
                Constants.DefaultPlatformLibrary);

            IEnumerable<ResolvedFile> resolvedFiles = new PublishAssembliesResolver(new MockPackageResolver())
                .Resolve(projectContext);

            resolvedFiles
                .Should()
                .BeEquivalentTo(expectedResolvedFiles);
        }

        [Theory]
        [MemberData("ProjectData1")]
        public void ItResolvesAssembliesFromProjectLockFilesWithCacheLayout(string projectName, string runtime, object[] expectedResolvedFiles)
        {
            LockFile lockFile = TestLockFiles.GetLockFile(projectName);
            ProjectContext projectContext = lockFile.CreateProjectContext(
                FrameworkConstants.CommonFrameworks.NetCoreApp10,
                runtime,
                Constants.DefaultPlatformLibrary);

            IEnumerable<ResolvedFile> resolvedFiles = new PublishAssembliesResolver(new MockPackageResolver())
                .WithPreserveCacheLayout(true)
                .Resolve(projectContext);

            resolvedFiles
                .Should()
                .BeEquivalentTo(expectedResolvedFiles);

        }

        public static IEnumerable<object[]> ProjectData
        {
            get
            {
                return new[]
                {
                    new object[] {
                        "dotnet.new",
                        null,
                        new object[] { }
                    },
                    new object[] {
                        "simple.dependencies",
                        null,
                        new object[] {
                            CreateResolvedFileForTFM("Newtonsoft.Json", "9.0.1", "netstandard1.0"),
                            CreateResolvedFileForTFM("System.Runtime.Serialization.Primitives", "4.1.1", "netstandard1.3"),
                            CreateResolvedFileForTFM("System.Collections.NonGeneric", "4.0.1", "netstandard1.3"),
                        }
                    },
                    new object[] {
                        "all.asset.types",
                        null,
                        new object[] {
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/osx/native/libuv.dylib"),
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/win7-x64/native/libuv.dll"),
                            CreateResolvedFileForTFM("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "de"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "zh-Hant"),
                        }
                    },
                    new object[] {
                        "all.asset.types",
                        "osx.10.11-x64",
                        new object[] {
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/osx/native/libuv.dylib", destinationSubDirectory: null, preserveCacheLayout: false),
                            CreateResolvedFileForTFM("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "de"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "zh-Hant"),
                        }
                    },
                };
            }
        }

        public static IEnumerable<object[]> ProjectData1
        {
            get
            {
                return new[]
                {
                    new object[] {
                        "dotnet.new",
                        null,
                        new object[] { }
                    },
                    new object[] {
                        "simple.dependencies",
                        null,
                        new object[] {
                            CreateResolvedFileForTFM("Newtonsoft.Json", "9.0.1", "netstandard1.0", true),
                            CreateResolvedFileForTFM("System.Runtime.Serialization.Primitives", "4.1.1", "netstandard1.3", true),
                            CreateResolvedFileForTFM("System.Collections.NonGeneric", "4.0.1", "netstandard1.3", true),
                        }
                    },
                    new object[] {
                        "all.asset.types",
                        null,
                        new object[] {
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/osx/native/libuv.dylib", true),
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/win7-x64/native/libuv.dll", true),
                            CreateResolvedFileForTFM("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", true),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "de", true),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "zh-Hant", true),
                        }
                    },
                    new object[] {
                        "all.asset.types",
                        "osx.10.11-x64",
                        new object[] {
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/osx/native/libuv.dylib", destinationSubDirectory: null, preserveCacheLayout: true),
                            CreateResolvedFileForTFM("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", true),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "de", true),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "zh-Hant", true),
                        }
                    },
                };
            }
        }
        private static ResolvedFile CreateResolvedFileForTFM(string packageId, string version, string tfm, bool preserveCacheLayout = false)
        {
            return CreateResolvedFile(packageId, version, $"lib/{tfm}/{packageId}.dll", null, preserveCacheLayout, AssetType.Runtime);
        }

        private static ResolvedFile CreateResourceResolvedFile(string packageId, string version, string tfm, string locale, bool preserveCacheLayout = false)
        {
            return CreateResolvedFile(packageId, version, $"lib/{tfm}/{locale}/{packageId}.resources.dll", locale, preserveCacheLayout, AssetType.Resources);
        }

        private static ResolvedFile CreateNativeResolvedFile(string packageId, string version, string filePath, bool preserveCacheLayout = false)
        {
            return CreateNativeResolvedFile(packageId, version, filePath, Path.GetDirectoryName(filePath), preserveCacheLayout);
        }

        private static ResolvedFile CreateNativeResolvedFile(
            string packageId,
            string version,
            string filePath,
            string destinationSubDirectory,
            bool preserveCacheLayout)
        {
            return CreateResolvedFile(packageId, version, filePath, destinationSubDirectory, preserveCacheLayout, AssetType.Native);
        }

        private static ResolvedFile CreateResolvedFile(
            string packageId,
            string version,
            string filePath,
            string destinationSubDirectory,
            bool preserveCacheLayout,
            AssetType assetType)
        {
            string packageRoot;
            string packageDirectory = new MockPackageResolver()
                .GetPackageDirectory(packageId, NuGetVersion.Parse(version), out packageRoot);

            assetType.Should().NotBe(AssetType.None);

            string sourcepath = Path.Combine(packageDirectory, filePath);
            string sourcedir = Path.GetDirectoryName(sourcepath);
            string destinationSubDirPath = preserveCacheLayout ? sourcedir.Substring(packageRoot.Length): destinationSubDirectory;
            return new ResolvedFile(
                sourcepath,
                destinationSubDirPath,
                assetType);
        }
    }
}
