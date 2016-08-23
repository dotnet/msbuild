// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.NETCore.Build.Tasks.UnitTests
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

            IEnumerable<ResolvedFile> resolvedFiles = new PublishAssembliesResolver(lockFile, new MockPackageResolver())
                .Resolve(
                    FrameworkConstants.CommonFrameworks.NetCoreApp10,
                    runtime);

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
                            CreateNativeResolvedFile("Libuv", "1.9.0", "runtimes/osx/native/libuv.dylib", destinationSubDirectory: null),
                            CreateResolvedFileForTFM("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "de"),
                            CreateResourceResolvedFile("System.Spatial", "5.7.0", "portable-net45+wp8+win8+wpa", "zh-Hant"),
                        }
                    },
                };
            }
        }

        private static ResolvedFile CreateResolvedFileForTFM(string packageId, string version, string tfm)
        {
            return CreateResolvedFile(packageId, version, $"lib/{tfm}/{packageId}.dll", null);
        }

        private static ResolvedFile CreateResourceResolvedFile(string packageId, string version, string tfm, string locale)
        {
            return CreateResolvedFile(packageId, version, $"lib/{tfm}/{locale}/{packageId}.resources.dll", locale);
        }

        private static ResolvedFile CreateNativeResolvedFile(string packageId, string version, string filePath)
        {
            return CreateNativeResolvedFile(packageId, version, filePath, Path.GetDirectoryName(filePath));
        }

        private static ResolvedFile CreateNativeResolvedFile(
            string packageId,
            string version,
            string filePath,
            string destinationSubDirectory)
        {
            return CreateResolvedFile(packageId, version, filePath, destinationSubDirectory);
        }

        private static ResolvedFile CreateResolvedFile(
            string packageId,
            string version,
            string filePath,
            string destinationSubDirectory)
        {
            string packageDirectory = new MockPackageResolver()
                .GetPackageDirectory(packageId, NuGetVersion.Parse(version));

            return new ResolvedFile(
                Path.Combine(packageDirectory, filePath),
                destinationSubDirectory);
        }

        private class MockPackageResolver : IPackageResolver
        {
            public string GetPackageDirectory(string packageId, NuGetVersion version)
            {
                return Path.Combine("/root", packageId, version.ToNormalizedString(), "path");
            }
        }
    }
}
