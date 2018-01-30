// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Engine.UnitTests;
using NuGet.Versioning;
using Shouldly;
using System.Collections.Generic;
using Xunit;

using SdkResolverContextBase = Microsoft.Build.Framework.SdkResolverContext;

namespace NuGet.MSBuildSdkResolver.UnitTests
{
    public class NuGetSdkResolver_Tests
    {
        [Fact]
        public void TryGetNuGetVersionForSdkGetsVersionFromGlobalJson()
        {
            Dictionary<string, string> expectedVersions = new Dictionary<string, string>
            {
                {"foo", "5.11.77"},
                {"bar", "2.0.0"}
            };

            using (TestEnvironment testEnvironment = TestEnvironment.Create())
            {
                TransientTestProjectWithFiles projectWithFiles = testEnvironment.CreateTestProjectWithFiles("", relativePathFromRootToProject: @"a\b\c");

                GlobalJsonReader_Tests.WriteGlobalJson(projectWithFiles.TestRoot, expectedVersions);

                MockSdkResolverContext context = new MockSdkResolverContext(projectWithFiles.ProjectFile);

                VerifyTryGetNuGetVersionForSdk(
                    version: null,
                    expectedVersion: NuGetVersion.Parse(expectedVersions["foo"]),
                    context: context);
            }
        }

        [Fact]
        public void TryGetNuGetVersionForSdkGetsVersionFromState()
        {
            MockSdkResolverContext context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>
                {
                    {"foo", "1.2.3"}
                }
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: NuGetVersion.Parse("1.2.3"),
                context: context);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkInvalidVersion()
        {
            VerifyTryGetNuGetVersionForSdk(
                version: "abc",
                expectedVersion: null);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkInvalidVersionInGlobalJson()
        {
            MockSdkResolverContext context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>
                {
                    {"foo", "abc"}
                }
            };

            VerifyTryGetNuGetVersionForSdk(
                version: "abc",
                expectedVersion: null,
                context: context);
        }

        [Fact]
        public void TryGetNuGetVersionForSdkSucceeds()
        {
            VerifyTryGetNuGetVersionForSdk(
                version: "3.2.1",
                expectedVersion: NuGetVersion.Parse("3.2.1"));
        }

        [Fact]
        public void TryGetNuGetVersionNoVersionSpecified()
        {
            MockSdkResolverContext context = new MockSdkResolverContext("foo.proj")
            {
                State = new Dictionary<string, string>()
            };

            VerifyTryGetNuGetVersionForSdk(
                version: null,
                expectedVersion: null,
                context: context);
        }

        private void VerifyTryGetNuGetVersionForSdk(string version, NuGetVersion expectedVersion, SdkResolverContextBase context = null)
        {
            object parsedVersion;

            bool result = NuGetSdkResolver.TryGetNuGetVersionForSdk("foo", version, context, out parsedVersion);

            if (expectedVersion != null)
            {
                result.ShouldBeTrue();

                parsedVersion.ShouldNotBeNull();

                parsedVersion.ShouldBe(expectedVersion);
            }
            else
            {
                result.ShouldBeFalse();

                parsedVersion.ShouldBeNull();
            }
        }
    }
}
