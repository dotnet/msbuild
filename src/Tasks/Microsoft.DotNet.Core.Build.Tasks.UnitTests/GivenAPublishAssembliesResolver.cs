// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using NuGet.Versioning;
using Xunit;

namespace Microsoft.DotNet.Core.Build.Tasks.UnitTests
{
    public class GivenAPublishAssembliesResolver
    {
        /// <summary>
        /// Tests that PublishAssembliesResolver resolves assemblies correctly.
        /// </summary>
        [Theory]
        [MemberData("ProjectData")]
        public void ItResolvesAssembliesFromProjectLockFiles(string projectName, object[] expectedResolvedFiles)
        {
            LockFile lockFile = LockFileUtilities.GetLockFile($"{projectName}.project.lock.json", NullLogger.Instance);

            IEnumerable<ResolvedFile> resolvedFiles = new PublishAssembliesResolver(lockFile, new MockPackageResolver())
                .Resolve(
                    FrameworkConstants.CommonFrameworks.NetCoreApp10,
                    runtime: null);

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
                        new object[] { }
                    },
                    new object[] {
                        "simple.dependencies",
                        new object[] {
                            CreateResolvedFile("Newtonsoft.Json", "9.0.1", "netstandard1.0"),
                            CreateResolvedFile("System.Runtime.Serialization.Primitives", "4.1.1", "netstandard1.3"),
                            CreateResolvedFile("System.Collections.NonGeneric", "4.0.1", "netstandard1.3"),
                        }
                    },
                };
            }
        }

        private static ResolvedFile CreateResolvedFile(string packageId, string version, string tfm)
        {
            string packageDirectory = new MockPackageResolver().GetPackageDirectory(packageId, NuGetVersion.Parse(version));

            return new ResolvedFile(
                Path.Combine(packageDirectory, $"lib/{tfm}/{packageId}.dll"),
                null);
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
