// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Basic tests of Platform.xml parsing
    /// </summary>
    public sealed class PlatformManifest_Tests
    {
        /// <summary>
        /// Should get a read error when the manifest location is invalid
        /// </summary>
        [Fact]
        public void InvalidManifestLocation()
        {
            PlatformManifest manifest = new PlatformManifest("|||||||");

            manifest.ReadError.ShouldBeTrue();
        }

        /// <summary>
        /// Should get a read error when the manifest location is valid but empty
        /// </summary>
        [Fact]
        public void EmptyManifestLocation()
        {
            string manifestDirectory = null;

            try
            {
                manifestDirectory = FileUtilities.GetTemporaryDirectory();
                PlatformManifest manifest = new PlatformManifest(manifestDirectory);

                manifest.ReadError.ShouldBeTrue();
            }
            finally
            {
                if (manifestDirectory != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(manifestDirectory, recursive: true);
                }
            }
        }

        /// <summary>
        /// Should get a read error when the manifest location is valid but doesn't have a
        /// file named Platform.xml
        /// </summary>
        [Fact]
        public void ManifestLocationHasNoPlatformXml()
        {
            string manifestDirectory = null;

            try
            {
                manifestDirectory = FileUtilities.GetTemporaryDirectory();
                File.WriteAllText(Path.Combine(manifestDirectory, "SomeOtherFile.xml"), "hello");
                PlatformManifest manifest = new PlatformManifest(manifestDirectory);

                manifest.ReadError.ShouldBeTrue();
            }
            finally
            {
                if (manifestDirectory != null)
                {
                    FileUtilities.DeleteDirectoryNoThrow(manifestDirectory, recursive: true);
                }
            }
        }

        /// <summary>
        /// Should get a read error when trying to read an invalid manifest file.
        /// </summary>
        [Fact]
        public void InvalidManifest()
        {
            string contents = @"|||||";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeTrue();
            }
        }

        /// <summary>
        /// Verify that a simple PlatformManifest can be successfully constructed.
        /// </summary>
        [Fact]
        public void SimpleValidManifest()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0` />";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();
                manifest.Manifest.Name.ShouldBe("UAP");
                manifest.Manifest.FriendlyName.ShouldBe("Universal Application Platform");
                manifest.Manifest.PlatformVersion.ShouldBe("1.0.0.0");

                manifest.Manifest.DependentPlatforms.Count.ShouldBe(0);
                manifest.Manifest.ApiContracts.Count.ShouldBe(0);
            }
        }

        /// <summary>
        /// Verify that a simple PlatformManifest can be successfully constructed, even if it's missing
        /// some fields.
        /// </summary>
        [Fact]
        public void SimpleValidManifestWithMissingFriendlyName()
        {
            string contents = @"<ApplicationPlatform name=`UAP` version=`1.0.0.0` />";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();
                manifest.Manifest.Name.ShouldBe("UAP");
                manifest.Manifest.FriendlyName.ShouldBe(string.Empty);
                manifest.Manifest.PlatformVersion.ShouldBe("1.0.0.0");

                manifest.Manifest.DependentPlatforms.Count.ShouldBe(0);
                manifest.Manifest.ApiContracts.Count.ShouldBe(0);
            }
        }

        /// <summary>
        /// Platform manifest with a dependent platform missing some information.
        /// NOTE: probably ought to be an error.
        /// </summary>
        [Fact]
        public void DependentPlatformMissingName()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <DependentPlatform version=`1.0.0.0` />
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();

                manifest.Manifest.ApiContracts.Count.ShouldBe(0);
                manifest.Manifest.DependentPlatforms.Count.ShouldBe(1);

                var platforms = new List<PlatformManifest.DependentPlatform>(manifest.Manifest.DependentPlatforms);
                platforms[0].Name.ShouldBe(string.Empty);
                platforms[0].Version.ShouldBe("1.0.0.0");
            }
        }

        /// <summary>
        /// Verify a PlatformManifest with multiple dependent platforms.
        /// </summary>
        [Fact]
        public void MultipleDependentPlatforms()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <DependentPlatform name=`UAP` version=`1.0.0.0` />
                                  <DependentPlatform name=`UAP` version=`1.0.2.3` />
                                  <DependentPlatform name=`MyPlatform` version=`8.8.8.8` />
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();

                manifest.Manifest.ApiContracts.Count.ShouldBe(0);
                manifest.Manifest.DependentPlatforms.Count.ShouldBe(3);

                var platforms = new List<PlatformManifest.DependentPlatform>(manifest.Manifest.DependentPlatforms);
                platforms[0].Name.ShouldBe("UAP");
                platforms[0].Version.ShouldBe("1.0.0.0");
                platforms[1].Name.ShouldBe("UAP");
                platforms[1].Version.ShouldBe("1.0.2.3");
                platforms[2].Name.ShouldBe("MyPlatform");
                platforms[2].Version.ShouldBe("8.8.8.8");
            }
        }

        /// <summary>
        /// Platform manifest with a contract missing some information.
        /// NOTE: technically probably ought to be an error.
        /// </summary>
        [Fact]
        public void ContractMissingVersion()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <DependentPlatform name=`UAP` version=`1.0.2.3` />
                                  <ContainedApiContracts>
                                    <ApiContract name=`System` />
                                  </ContainedApiContracts>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();

                manifest.Manifest.DependentPlatforms.Count.ShouldBe(1);
                PlatformManifest.DependentPlatform platform = manifest.Manifest.DependentPlatforms.First();
                platform.Name.ShouldBe("UAP");
                platform.Version.ShouldBe("1.0.2.3");

                manifest.Manifest.ApiContracts.Count.ShouldBe(1);
                ApiContract contract = manifest.Manifest.ApiContracts.First();
                contract.Name.ShouldBe("System");
                contract.Version.ShouldBe(string.Empty);
            }
        }

        /// <summary>
        /// Verify a platform manifest with API contracts.
        /// </summary>
        [Fact]
        public void MultipleContracts()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <ContainedApiContracts>
                                    <ApiContract name=`System` version=`1.2.0.4` />
                                    <ApiContract name=`Windows.Foundation` version=`1.0.0.0` />
                                    <ApiContract name=`Windows.Foundation.OtherStuff` version=`1.5.0.0` />
                                  </ContainedApiContracts>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.ReadError.ShouldBeFalse();

                manifest.Manifest.DependentPlatforms.Count.ShouldBe(0);
                manifest.Manifest.ApiContracts.Count.ShouldBe(3);

                var contracts = new List<ApiContract>(manifest.Manifest.ApiContracts);

                contracts[0].Name.ShouldBe("System");
                contracts[0].Version.ShouldBe("1.2.0.4");
                contracts[1].Name.ShouldBe("Windows.Foundation");
                contracts[1].Version.ShouldBe("1.0.0.0");
                contracts[2].Name.ShouldBe("Windows.Foundation.OtherStuff");
                contracts[2].Version.ShouldBe("1.5.0.0");
            }
        }

        [Fact]
        public void VersionedContentFlagMissingReturnsFalse()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.VersionedContent.ShouldBeFalse();
            }
        }

        [Fact]
        public void VersionedContentInvalidFlagReturnsFalse()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                    <VersionedContent>Invalid</VersionedContent>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.VersionedContent.ShouldBeFalse();
            }
        }

        [Fact]
        public void VersionedContentFalseFlagReturnsFalse()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                    <VersionedContent>False</VersionedContent>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.VersionedContent.ShouldBeFalse();
            }
        }

        [Fact]
        public void VersionedContentTrueFlagReturnsTrue()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                    <VersionedContent>True</VersionedContent>
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                manifest.Manifest.VersionedContent.ShouldBeTrue();
            }
        }

        /// <summary>
        /// Wrapper around PlatformManifest that creates one with the specified content in
        /// the temporary directory and deletes it on disposal.
        /// </summary>
        private sealed class TemporaryPlatformManifest : IDisposable
        {
            /// <summary>
            /// Directory in which the PlatformManifest wrapped by this class lives
            /// </summary>
            private readonly string _manifestDirectory;

            /// <summary>
            /// Accessor for the PlatformManifest wrapped by this class
            /// </summary>
            public PlatformManifest Manifest { get; }

            /// <summary>
            /// Constructor
            /// </summary>
            public TemporaryPlatformManifest(string contents)
            {
                _manifestDirectory = FileUtilities.GetTemporaryDirectory();
                File.WriteAllText(Path.Combine(_manifestDirectory, "Platform.xml"), ObjectModelHelpers.CleanupFileContents(contents));

                Manifest = new PlatformManifest(_manifestDirectory);
            }

            public void Dispose()
            {
                if (FileUtilities.DirectoryExistsNoThrow(_manifestDirectory))
                {
                    FileUtilities.DeleteDirectoryNoThrow(_manifestDirectory, recursive: true);
                }
            }
        }
    }
}
