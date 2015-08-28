// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Basic tests of Platform.xml parsing
    /// </summary>
    [TestClass]
    sealed public class PlatformManifest_Tests
    {
        /// <summary>
        /// Should get a read error when the manifest location is invalid
        /// </summary>
        [TestMethod]
        public void InvalidManifestLocation()
        {
            PlatformManifest manifest = new PlatformManifest("|||||||");

            Assert.IsTrue(manifest.ReadError);
        }

        /// <summary>
        /// Should get a read error when the manifest location is valid but empty
        /// </summary>
        [TestMethod]
        public void EmptyManifestLocation()
        {
            string manifestDirectory = null;

            try
            {
                manifestDirectory = FileUtilities.GetTemporaryDirectory();
                PlatformManifest manifest = new PlatformManifest(manifestDirectory);

                Assert.IsTrue(manifest.ReadError);
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
        [TestMethod]
        public void ManifestLocationHasNoPlatformXml()
        {
            string manifestDirectory = null;

            try
            {
                manifestDirectory = FileUtilities.GetTemporaryDirectory();
                File.WriteAllText(Path.Combine(manifestDirectory, "SomeOtherFile.xml"), "hello");
                PlatformManifest manifest = new PlatformManifest(manifestDirectory);

                Assert.IsTrue(manifest.ReadError);
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
        [TestMethod]
        public void InvalidManifest()
        {
            string contents = @"|||||";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                Assert.IsTrue(manifest.Manifest.ReadError);
            }
        }

        /// <summary>
        /// Verify that a simple PlatformManifest can be successfully constructed. 
        /// </summary>
        [TestMethod]
        public void SimpleValidManifest()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0` />";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                Assert.IsFalse(manifest.Manifest.ReadError);
                Assert.AreEqual("UAP", manifest.Manifest.Name);
                Assert.AreEqual("Universal Application Platform", manifest.Manifest.FriendlyName);
                Assert.AreEqual("1.0.0.0", manifest.Manifest.PlatformVersion);

                Assert.AreEqual(0, manifest.Manifest.DependentPlatforms.Count);
                Assert.AreEqual(0, manifest.Manifest.ApiContracts.Count);
            }
        }

        /// <summary>
        /// Verify that a simple PlatformManifest can be successfully constructed, even if it's missing 
        /// some fields. 
        /// </summary>
        [TestMethod]
        public void SimpleValidManifestWithMissingFriendlyName()
        {
            string contents = @"<ApplicationPlatform name=`UAP` version=`1.0.0.0` />";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                Assert.IsFalse(manifest.Manifest.ReadError);
                Assert.AreEqual("UAP", manifest.Manifest.Name);
                Assert.AreEqual(String.Empty, manifest.Manifest.FriendlyName);
                Assert.AreEqual("1.0.0.0", manifest.Manifest.PlatformVersion);

                Assert.AreEqual(0, manifest.Manifest.DependentPlatforms.Count);
                Assert.AreEqual(0, manifest.Manifest.ApiContracts.Count);
            }
        }

        /// <summary>
        /// Platform manifest with a dependent platform missing some information. 
        /// NOTE: probably ought to be an error. 
        /// </summary>
        [TestMethod]
        public void DependentPlatformMissingName()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <DependentPlatform version=`1.0.0.0` />
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                Assert.IsFalse(manifest.Manifest.ReadError);

                Assert.AreEqual(0, manifest.Manifest.ApiContracts.Count);
                Assert.AreEqual(1, manifest.Manifest.DependentPlatforms.Count);

                List<PlatformManifest.DependentPlatform> platforms = new List<PlatformManifest.DependentPlatform>(manifest.Manifest.DependentPlatforms);
                Assert.AreEqual(String.Empty, platforms[0].Name);
                Assert.AreEqual("1.0.0.0", platforms[0].Version);
            }
        }

        /// <summary>
        /// Verify a PlatformManifest with multiple dependent platforms.
        /// </summary>
        [TestMethod]
        public void MultipleDependentPlatforms()
        {
            string contents = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                  <DependentPlatform name=`UAP` version=`1.0.0.0` />
                                  <DependentPlatform name=`UAP` version=`1.0.2.3` />
                                  <DependentPlatform name=`MyPlatform` version=`8.8.8.8` />
                                </ApplicationPlatform>";

            using (TemporaryPlatformManifest manifest = new TemporaryPlatformManifest(contents))
            {
                Assert.IsFalse(manifest.Manifest.ReadError);

                Assert.AreEqual(0, manifest.Manifest.ApiContracts.Count);
                Assert.AreEqual(3, manifest.Manifest.DependentPlatforms.Count);

                List<PlatformManifest.DependentPlatform> platforms = new List<PlatformManifest.DependentPlatform>(manifest.Manifest.DependentPlatforms);
                Assert.AreEqual("UAP", platforms[0].Name);
                Assert.AreEqual("1.0.0.0", platforms[0].Version);
                Assert.AreEqual("UAP", platforms[1].Name);
                Assert.AreEqual("1.0.2.3", platforms[1].Version);
                Assert.AreEqual("MyPlatform", platforms[2].Name);
                Assert.AreEqual("8.8.8.8", platforms[2].Version);
            }
        }

        /// <summary>
        /// Platform manifest with a contract missing some information. 
        /// NOTE: technically probably ought to be an error. 
        /// </summary>
        [TestMethod]
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
                Assert.IsFalse(manifest.Manifest.ReadError);

                Assert.AreEqual(1, manifest.Manifest.DependentPlatforms.Count);
                PlatformManifest.DependentPlatform platform = manifest.Manifest.DependentPlatforms.First();
                Assert.AreEqual("UAP", platform.Name);
                Assert.AreEqual("1.0.2.3", platform.Version);

                Assert.AreEqual(1, manifest.Manifest.ApiContracts.Count);
                ApiContract contract = manifest.Manifest.ApiContracts.First();
                Assert.AreEqual("System", contract.Name);
                Assert.AreEqual(String.Empty, contract.Version);
            }
        }

        /// <summary>
        /// Verify a platform manifest with API contracts. 
        /// </summary>
        [TestMethod]
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
                Assert.IsFalse(manifest.Manifest.ReadError);

                Assert.AreEqual(0, manifest.Manifest.DependentPlatforms.Count);
                Assert.AreEqual(3, manifest.Manifest.ApiContracts.Count);

                List<ApiContract> contracts = new List<ApiContract>(manifest.Manifest.ApiContracts);

                Assert.AreEqual("System", contracts[0].Name);
                Assert.AreEqual("1.2.0.4", contracts[0].Version);
                Assert.AreEqual("Windows.Foundation", contracts[1].Name);
                Assert.AreEqual("1.0.0.0", contracts[1].Version);
                Assert.AreEqual("Windows.Foundation.OtherStuff", contracts[2].Name);
                Assert.AreEqual("1.5.0.0", contracts[2].Version);
            }
        }

        /// <summary>
        /// Wrapper around PlatformManifest that creates one with the specified content in 
        /// the temporary directory and deletes it on disposal. 
        /// </summary>
        private class TemporaryPlatformManifest : IDisposable
        {
            /// <summary>
            /// Directory in which the PlatformManifest wrapped by this class lives
            /// </summary>
            private string _manifestDirectory = null;

            /// <summary>
            /// Accessor for the PlatformManifest wrapped by this class
            /// </summary>
            public PlatformManifest Manifest
            {
                get;
                private set;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            public TemporaryPlatformManifest(string contents)
            {
                _manifestDirectory = FileUtilities.GetTemporaryDirectory();
                File.WriteAllText(Path.Combine(_manifestDirectory, "Platform.xml"), ObjectModelHelpers.CleanupFileContents(contents));

                Manifest = new PlatformManifest(_manifestDirectory);
            }

            #region IDisposable Support

            /// <summary>
            /// Dispose this object
            /// </summary>
            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (FileUtilities.DirectoryExistsNoThrow(_manifestDirectory))
                    {
                        FileUtilities.DeleteDirectoryNoThrow(_manifestDirectory, recursive: true);
                    }
                }
            }

            /// <summary>
            /// Dispose this object
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
            }
            #endregion

        }
    }
}
