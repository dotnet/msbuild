// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class GetTargetPlatformReferences_Tests
    {
        /// <summary>
        /// Location of the fake SDK structure
        /// </summary>
        private static string s_fakeStructureRoot = null;

        /// <summary>
        /// Setup the fake SDK structure used by these tests
        /// </summary>
        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            s_fakeStructureRoot = MakeFakeSDKStructure();
        }

        /// <summary>
        /// Clean up the fake SDK structure used by these tests
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_fakeStructureRoot != null)
            {
                if (FileUtilities.DirectoryExistsNoThrow(s_fakeStructureRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(s_fakeStructureRoot, true);
                }
            }
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns nothing when there's no matching root SDK
        /// </summary>
        [TestMethod]
        public void NoRootSdk()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("MissingSDK", "1.1", "MissingPlatform", "0.0.0.0", "0.0.0.0", s_fakeStructureRoot, null);
            Assert.AreEqual(0, winmds.Length);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences still returns valid output when passed a legacy-style SDK
        /// </summary>
        [TestMethod]
        public void LegacySdk()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences(null, null, "OldSdk", null, "5.0", s_fakeStructureRoot, null);

            Assert.AreEqual(2, winmds.Length);
            Assert.AreEqual(Path.Combine(s_fakeStructureRoot, "OldSdk\\5.0\\References\\CommonConfiguration\\Neutral\\Another.winmd"), winmds[0]);
            Assert.AreEqual(Path.Combine(s_fakeStructureRoot, "OldSdk\\5.0\\References\\CommonConfiguration\\Neutral\\Windows.winmd"), winmds[1]);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns nothing when there's no matching platforms directory
        /// </summary>
        [TestMethod]
        public void MissingPlatformDirectory()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "MissingPlatform", "0.0.0.0", "0.0.0.0", s_fakeStructureRoot, null);
            Assert.AreEqual(0, winmds.Length);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns nothing when there's no Platform.xml in the platforms directory
        /// </summary>
        [TestMethod]
        public void MissingPlatformXml()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "NoXml", "0.0.0.0", "1.0.1.0", s_fakeStructureRoot, null);
            Assert.AreEqual(0, winmds.Length);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns nothing when there's an invalid Platform.xml in the platforms directory
        /// </summary>
        [TestMethod]
        public void BadPlatformXml()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "BadXml", "0.0.0.0", "1.0.2.2", s_fakeStructureRoot, null);
            Assert.AreEqual(0, winmds.Length);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns nothing when the platform.xml is valid but there are no contracts defined
        /// </summary>
        [TestMethod]
        public void NoContracts()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "NoContracts", "0.0.0.0", "3.0.5.4", s_fakeStructureRoot, null);
            Assert.AreEqual(0, winmds.Length);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns all winmds defined in the contracts 
        /// </summary>
        [TestMethod]
        public void ValidContracts()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "SomeContracts", "0.0.0.0", "1.0.9.9", s_fakeStructureRoot, null);
            string referencesRoot = Path.Combine(s_fakeStructureRoot, "RootSdk\\1.1\\References");

            Assert.AreEqual(4, winmds.Length);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0\\Windows.Core.winmd"), winmds[0]);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Foundation\\1.0.0.0\\OtherNamesAreStillFine.winmd"), winmds[1]);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Foundation.OtherStuff\\1.5.0.0\\MultipleWinmdsAreFine.winmd"), winmds[2]);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Foundation.OtherStuff\\1.5.0.0\\Windows.Foundation.OtherStuff.winmd"), winmds[3]);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns all winmds defined in the valid contracts, but ignores missing 
        /// contract directories
        /// </summary>
        [TestMethod]
        public void MissingContract()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "SomeContracts", "0.0.0.0", "2.3.4.5", s_fakeStructureRoot, null);
            string referencesRoot = Path.Combine(s_fakeStructureRoot, "RootSdk\\1.1\\References");

            Assert.AreEqual(1, winmds.Length);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0\\Windows.Core.winmd"), winmds[0]);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns all winmds defined in the valid contracts, but ignores empty 
        /// contract directories
        /// </summary>
        [TestMethod]
        public void EmptyContract()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "SomeContracts", "0.0.0.0", "3.9.9.4", s_fakeStructureRoot, null);
            string referencesRoot = Path.Combine(s_fakeStructureRoot, "RootSdk\\1.1\\References");

            Assert.AreEqual(1, winmds.Length);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0\\Windows.Core.winmd"), winmds[0]);
        }

        /// <summary>
        /// Verify GetTargetPlatformReferences returns all winmds defined in the valid contracts, but ignores  
        /// contract directories that don't contain winmds
        /// </summary>
        [TestMethod]
        public void NonEmptyContractFolderButNoWinMDs()
        {
            string[] winmds = ToolLocationHelper.GetTargetPlatformReferences("RootSdk", "1.1", "SomeContracts", "0.0.0.0", "4.3.2.8", s_fakeStructureRoot, null);
            string referencesRoot = Path.Combine(s_fakeStructureRoot, "RootSdk\\1.1\\References");

            Assert.AreEqual(1, winmds.Length);
            Assert.AreEqual(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0\\Windows.Core.winmd"), winmds[0]);
        }

        /// <summary>
        /// Generate a fake SDK structure 
        /// </summary>
        /// <returns></returns>
        private static string MakeFakeSDKStructure()
        {
            // no contracts 
            string platformManifest1 = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                                     <ContainedApiContracts />
                                                   </ApplicationPlatform>";

            // some contracts
            string platformManifest2 = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                                       <ContainedApiContracts>
                                                         <ApiContract name=`Windows.Core` version=`0.7.0.0` />
                                                         <ApiContract name=`Windows.Foundation` version=`1.0.0.0` />
                                                         <ApiContract name=`Windows.Foundation.OtherStuff` version=`1.5.0.0` />
                                                       </ContainedApiContracts>
                                                     </ApplicationPlatform>";

            // one missing contract, other good
            string platformManifest3 = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                                       <ContainedApiContracts>
                                                         <ApiContract name=`Windows.Core` version=`0.7.0.0` />
                                                         <ApiContract name=`Missing` version=`0.8.0.0` />
                                                       </ContainedApiContracts>
                                                     </ApplicationPlatform>";

            // one empty contracts directory, other good
            string platformManifest4 = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                                       <ContainedApiContracts>
                                                         <ApiContract name=`Windows.Core` version=`0.7.0.0` />
                                                         <ApiContract name=`Empty` version=`0.8.0.0` />
                                                       </ContainedApiContracts>
                                                     </ApplicationPlatform>";

            // one contracts directory without winmds in it, other good
            string platformManifest5 = @"<ApplicationPlatform name=`UAP` friendlyName=`Universal Application Platform` version=`1.0.0.0`>
                                                       <ContainedApiContracts>
                                                         <ApiContract name=`Windows.Core` version=`0.7.0.0` />
                                                         <ApiContract name=`NonEmptyButNoWinmds` version=`0.8.0.0` />
                                                       </ContainedApiContracts>
                                                     </ApplicationPlatform>";

            string fakeSdkRoot = FileUtilities.GetTemporaryDirectory();

            // Legacy-style SDK
            string oldSdkRoot = Path.Combine(fakeSdkRoot, "OldSdk\\5.0");
            Directory.CreateDirectory(oldSdkRoot);
            File.WriteAllText(Path.Combine(oldSdkRoot, "SDKManifest.xml"), "Hello");
            Directory.CreateDirectory(Path.Combine(oldSdkRoot, "References\\CommonConfiguration\\Neutral"));
            File.WriteAllText(Path.Combine(oldSdkRoot, "References\\CommonConfiguration\\Neutral\\Windows.winmd"), "Hello");
            File.WriteAllText(Path.Combine(oldSdkRoot, "References\\CommonConfiguration\\Neutral\\Another.winmd"), "Hello");

            // OneCore-style SDK
            string rootSdkRoot = Path.Combine(fakeSdkRoot, "RootSdk\\1.1");
            Directory.CreateDirectory(rootSdkRoot);
            File.WriteAllText(Path.Combine(rootSdkRoot, "SDKManifest.xml"), "Hello");

            // -- References --
            string referencesRoot = Path.Combine(rootSdkRoot, "References");

            Directory.CreateDirectory(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0"));
            File.WriteAllText(Path.Combine(referencesRoot, "Windows.Core\\0.7.0.0\\Windows.Core.winmd"), "Hello");

            Directory.CreateDirectory(Path.Combine(referencesRoot, "Windows.Foundation\\1.0.0.0"));
            File.WriteAllText(Path.Combine(referencesRoot, "Windows.Foundation\\1.0.0.0\\OtherNamesAreStillFine.winmd"), "Hello");

            Directory.CreateDirectory(Path.Combine(referencesRoot, "Windows.Foundation.OtherStuff\\1.5.0.0"));
            File.WriteAllText(Path.Combine(referencesRoot, "Windows.Foundation.OtherStuff\\1.5.0.0\\Windows.Foundation.OtherStuff.winmd"), "Hello");
            File.WriteAllText(Path.Combine(referencesRoot, "Windows.Foundation.OtherStuff\\1.5.0.0\\MultipleWinmdsAreFine.winmd"), "Hello");

            Directory.CreateDirectory(Path.Combine(referencesRoot, "Empty\\0.8.0.0"));

            Directory.CreateDirectory(Path.Combine(referencesRoot, "NonEmptyButNoWinmds\\0.8.0.0"));
            File.WriteAllText(Path.Combine(referencesRoot, "NonEmptyButNoWinmds\\0.8.0.0\\NonEmptyButNoWinmds.xml"), "Hello");

            // -- Platforms --
            string platformsRoot = Path.Combine(rootSdkRoot, "Platforms");

            // bad: missing platform.xml 
            Directory.CreateDirectory(Path.Combine(platformsRoot, "NoXml\\1.0.1.0"));

            // bad: invalid platform.xml
            Directory.CreateDirectory(Path.Combine(platformsRoot, "BadXml\\1.0.2.2"));
            File.WriteAllText(Path.Combine(platformsRoot, "BadXml\\1.0.2.2\\Platform.xml"), "||||");

            // good: no contracts
            Directory.CreateDirectory(Path.Combine(platformsRoot, "NoContracts\\3.0.5.4"));
            CleanupAndWrite(Path.Combine(platformsRoot, "NoContracts\\3.0.5.4\\Platform.xml"), platformManifest1);

            // good: all contracts exist
            Directory.CreateDirectory(Path.Combine(platformsRoot, "SomeContracts\\1.0.9.9"));
            CleanupAndWrite(Path.Combine(platformsRoot, "SomeContracts\\1.0.9.9\\Platform.xml"), platformManifest2);

            // partially good: one missing contract
            Directory.CreateDirectory(Path.Combine(platformsRoot, "SomeContracts\\2.3.4.5"));
            CleanupAndWrite(Path.Combine(platformsRoot, "SomeContracts\\2.3.4.5\\Platform.xml"), platformManifest3);

            // partially good: one empty contract directory
            Directory.CreateDirectory(Path.Combine(platformsRoot, "SomeContracts\\3.9.9.4"));
            CleanupAndWrite(Path.Combine(platformsRoot, "SomeContracts\\3.9.9.4\\Platform.xml"), platformManifest4);

            // partially good: one contract directory without winmds in it 
            Directory.CreateDirectory(Path.Combine(platformsRoot, "SomeContracts\\4.3.2.8"));
            CleanupAndWrite(Path.Combine(platformsRoot, "SomeContracts\\4.3.2.8\\Platform.xml"), platformManifest5);

            return fakeSdkRoot;
        }

        /// <summary>
        /// Fixes up the contents passed to it and writes the updated string to disk
        /// </summary>
        private static void CleanupAndWrite(string path, string content)
        {
            File.WriteAllText(path, ObjectModelHelpers.CleanupFileContents(content));
        }
    }
}
