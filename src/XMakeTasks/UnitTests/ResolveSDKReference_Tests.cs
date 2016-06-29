﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ResolveSDKReferences_Tests.cs" company="Microsoft">
// </copyright>
// <summary>Tests for the task that resolves an SDKReference to a full path on disk</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using SDKReference = Microsoft.Build.Tasks.ResolveSDKReference.SDKReference;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveSDKReference_Tests
{
    public class ResolveSDKReferenceTestFixture
    {
        private Microsoft.Build.UnitTests.MockEngine.GetStringDelegate _resourceDelegate = new Microsoft.Build.UnitTests.MockEngine.GetStringDelegate(AssemblyResources.GetString);
        #region TestMethods

        /// <summary>
        /// Make sure that SDK reference which should be good are parsed correctly.
        /// </summary>
        [Fact]
        public void ParseItemSpecGood()
        {
            TestGoodSDKReferenceIncludes(new TaskItem("Cat, Version=8.0"), "Cat", "8.0");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat, Version=   8.0"), "Cat", "8.0");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat, Version=8.0   "), "Cat", "8.0");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat, Version=8.0.255"), "Cat", "8.0.255");
            TestGoodSDKReferenceIncludes(new TaskItem("   Cat, Version=8.0.255"), "Cat", "8.0.255");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat   , Version=8.0.255"), "Cat", "8.0.255");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat,Version=8.0.255"), "Cat", "8.0.255");
            TestGoodSDKReferenceIncludes(new TaskItem("Cat, Version=8.0.255"), "Cat", "8.0.255");
        }

        /// <summary>
        /// Make sure ones which are incorrect and log the correct error.
        /// </summary>
        [Fact]
        public void ParseItemSpecBadNames()
        {
            //These should all be bad the format must be   <SDKName>, Version=<SDKVersion>.
            TestBadSDKReferenceIncludes(new TaskItem(""));
            TestBadSDKReferenceIncludes(new TaskItem("Cat, Version=8"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat 8.0"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat Version=8.0"));
            TestBadSDKReferenceIncludes(new TaskItem("Dog, Cat, Version=8.0"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat, Version=8.0, Moose"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat Version=v8.0"));
            TestBadSDKReferenceIncludes(new TaskItem(" , Version=8.0"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat, Version=v8.0"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat, Version=8.0.344.555.666.777.666.555.444"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat,"));
            TestBadSDKReferenceIncludes(new TaskItem("Cat, Version="));
        }

        /// <summary>
        /// Make sure ones which are incorrect and log the correct error.
        /// </summary>
        [Fact]
        public void ParseDependsOnString()
        {
            Assert.Equal(0, ResolveSDKReference.ParseDependsOnSDK(null).Count);
            Assert.Equal(0, ResolveSDKReference.ParseDependsOnSDK(String.Empty).Count);
            Assert.Equal(0, ResolveSDKReference.ParseDependsOnSDK(";;").Count);
            Assert.Equal(0, ResolveSDKReference.ParseDependsOnSDK("; ;").Count);

            List<string> parsedDependencies = ResolveSDKReference.ParseDependsOnSDK("; foo ;");
            Assert.Equal(1, parsedDependencies.Count);
            Assert.True(parsedDependencies[0].Equals("foo", StringComparison.OrdinalIgnoreCase));

            parsedDependencies = ResolveSDKReference.ParseDependsOnSDK(";;;bar, Version=1.0 ; ; ; foo, Version=2.0   ;;;;;;");
            Assert.Equal(2, parsedDependencies.Count);
            Assert.True(parsedDependencies[0].Equals("bar, Version=1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(parsedDependencies[1].Equals("foo, Version=2.0", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure ones which are incorrect and log the correct error.
        /// </summary>
        [Fact]
        public void GetUnResolvedDependentSDKs()
        {
            string[] result = null;
            HashSet<SDKReference> resolvedSDKsEmpty = new HashSet<SDKReference>();
            List<string> dependentSDKsEmpty = new List<string>();

            HashSet<SDKReference> resolvedSDKs = new HashSet<SDKReference>() { new SDKReference(new TaskItem(), "bar", "1.0"), new SDKReference(new TaskItem(), "foo", "1.0"), new SDKReference(new TaskItem(), "Newt", "1.0") };
            List<string> dependentSDKs = new List<string>() { "bar, Version=1.0", "bar, Version=2.0", "baz, Version=2.0", "CannotParseMeAsSDK", "newt, version=1.0" };

            result = ResolveSDKReference.GetUnresolvedDependentSDKs(resolvedSDKsEmpty, dependentSDKsEmpty);
            Assert.Equal(0, result.Length);

            result = ResolveSDKReference.GetUnresolvedDependentSDKs(new HashSet<SDKReference>(), dependentSDKs);
            Assert.Equal(4, result.Length);
            Assert.True(result[0].Equals("\"bar, Version=1.0\"", StringComparison.OrdinalIgnoreCase));
            Assert.True(result[1].Equals("\"bar, Version=2.0\"", StringComparison.OrdinalIgnoreCase));
            Assert.True(result[2].Equals("\"baz, Version=2.0\"", StringComparison.OrdinalIgnoreCase));
            Assert.True(result[3].Equals("\"newt, Version=1.0\"", StringComparison.OrdinalIgnoreCase));

            result = ResolveSDKReference.GetUnresolvedDependentSDKs(resolvedSDKs, dependentSDKsEmpty);
            Assert.Equal(0, result.Length);

            result = ResolveSDKReference.GetUnresolvedDependentSDKs(resolvedSDKs, dependentSDKs);
            Assert.Equal(2, result.Length);
            Assert.True(result[0].Equals("\"bar, Version=2.0\"", StringComparison.OrdinalIgnoreCase));
            Assert.True(result[1].Equals("\"baz, Version=2.0\"", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void VerifyBuildWarningForESDKWithoutMaxPlatformVersionOnBlueOrAbove()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "TestMaxPlatformVersionWithTargetFrameworkVersion");
            string testDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");

            // manifest does not contain MaxPlatformVersion
            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK'
                FrameworkIdentity = ''
                PlatformIdentity = 'windows'
                APPX = ''
                SDKType=''
                CopyRedistToSubDirectory=''
                SupportedArchitectures=''
                ProductFamilyName=''
                SupportsMultipleVersions=''
                ArchitectureForRuntime = ''
                DependsOn = ''
                MinOSVersion = ''
                MaxOSVersionTested = ''
               >
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            // manifest contains MaxPlatformVersion
            string sdkManifestContents2 =
            @"<FileList
                Identity = 'BadTestSDK, Version=1.0'
                DisplayName = 'BadTestSDK'
                FrameworkIdentity = ''
                PlatformIdentity = 'windows'
                APPX = ''
                SDKType=''
                CopyRedistToSubDirectory=''
                SupportedArchitectures=''
                ProductFamilyName=''
                SupportsMultipleVersions=''
                ArchitectureForRuntime = ''
                DependsOn = ''
                MinOSVersion = ''
                MaxOSVersionTested = ''
                MaxPlatformVersion = '8.1'
               >
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";


            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                installLocation.SetMetadata("PlatformVersion", "8.0");

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);

                // Resolve with PlatformVersion 7.0
                MockEngine engine1 = new MockEngine();
                TaskLoggingHelper log1 = new TaskLoggingHelper(engine1, "ResolveSDKReference");
                log1.TaskResources = AssemblyResources.PrimaryResources;

                ResolveSDKReference t1 = new ResolveSDKReference();
                t1.SDKReferences = new ITaskItem[] { item };
                t1.InstalledSDKs = new ITaskItem[] { installLocation };
                t1.WarnOnMissingPlatformVersion = true;
                t1.BuildEngine = engine1;
                t1.TargetPlatformVersion = "7.0";
                t1.ProjectName = "project.proj";
                t1.TargetPlatformIdentifier = "windows";
                bool succeeded1 = t1.Execute();

                Assert.True(succeeded1);
                engine1.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "GoodTestSDK", "2.0", "windows", "8.0", "windows", t1.TargetPlatformVersion);

                // Resolve with PlatformVersion 8.0
                MockEngine engine2 = new MockEngine();
                TaskLoggingHelper log2 = new TaskLoggingHelper(engine2, "ResolveSDKReference");
                log2.TaskResources = AssemblyResources.PrimaryResources;

                ResolveSDKReference t2 = new ResolveSDKReference();
                t2.SDKReferences = new ITaskItem[] { item };
                t2.InstalledSDKs = new ITaskItem[] { installLocation };
                t2.WarnOnMissingPlatformVersion = true;
                t2.BuildEngine = engine2;
                t2.TargetPlatformVersion = "8.0";
                t2.ProjectName = "project.proj";
                t2.TargetPlatformIdentifier = "windows";
                bool succeeded2 = t2.Execute();

                Assert.True(succeeded2);
                engine2.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "GoodTestSDK", "2.0", "windows", "8.0", "windows", t2.TargetPlatformVersion);

                // Resolve with PlatformVersion 8.1
                MockEngine engine3 = new MockEngine();
                TaskLoggingHelper log3 = new TaskLoggingHelper(engine3, "ResolveSDKReference");
                log3.TaskResources = AssemblyResources.PrimaryResources;

                ResolveSDKReference t3 = new ResolveSDKReference();
                t3.SDKReferences = new ITaskItem[] { item };
                t3.InstalledSDKs = new ITaskItem[] { installLocation };
                t3.WarnOnMissingPlatformVersion = true;
                t3.BuildEngine = engine3;
                t3.TargetPlatformVersion = "8.1";
                t3.ProjectName = "project.proj";
                t3.TargetPlatformIdentifier = "windows";
                bool succeeded3 = t3.Execute();

                Assert.True(succeeded3);
                engine3.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "GoodTestSDK", "2.0", "windows", "8.0", "windows", t3.TargetPlatformVersion);

                // Resolve with PlatformVersion 8.1 with WarnOnMissingPlatformVersion = false
                MockEngine engine3a = new MockEngine();
                TaskLoggingHelper log3a = new TaskLoggingHelper(engine3a, "ResolveSDKReference");
                log3a.TaskResources = AssemblyResources.PrimaryResources;

                ResolveSDKReference t3a = new ResolveSDKReference();
                t3a.SDKReferences = new ITaskItem[] { item };
                t3a.InstalledSDKs = new ITaskItem[] { installLocation };
                t3a.WarnOnMissingPlatformVersion = false;
                t3a.BuildEngine = engine3a;
                t3a.TargetPlatformVersion = "8.1";
                t3a.ProjectName = "project.proj";
                t3a.TargetPlatformIdentifier = "windows";
                bool succeeded3a = t3a.Execute();

                Assert.True(succeeded3a);
                engine3a.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "GoodTestSDK", "2.0", "windows", "8.0", "windows", t3a.TargetPlatformVersion);

                FileUtilities.DeleteNoThrow(sdkManifestFile);
                // Manifest with MaxPlatformVersion
                File.WriteAllText(sdkManifestFile, sdkManifestContents2);

                // Resolve with PlatformVersion 8.0
                MockEngine engine4 = new MockEngine();
                TaskLoggingHelper log4 = new TaskLoggingHelper(engine4, "ResolveSDKReference");
                log4.TaskResources = AssemblyResources.PrimaryResources;
                ResolveSDKReference t4 = new ResolveSDKReference();
                t4.SDKReferences = new ITaskItem[] { item };
                t4.InstalledSDKs = new ITaskItem[] { installLocation };
                t4.WarnOnMissingPlatformVersion = true;
                t4.BuildEngine = engine4;
                t4.TargetPlatformVersion = "8.0";
                t4.ProjectName = "project.proj";
                t4.TargetPlatformIdentifier = "windows";
                bool succeeded4 = t4.Execute();

                Assert.True(succeeded4);
                engine4.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "BadTestSDK", "1.0", "windows", "8.0", "windows", t4.TargetPlatformVersion);

                // Resolve with PlatformVersion 8.1
                MockEngine engine5 = new MockEngine();
                TaskLoggingHelper log5 = new TaskLoggingHelper(engine5, "ResolveSDKReference");
                log5.TaskResources = AssemblyResources.PrimaryResources;
                ResolveSDKReference t5 = new ResolveSDKReference();
                t5.SDKReferences = new ITaskItem[] { item };
                t5.InstalledSDKs = new ITaskItem[] { installLocation };
                t5.WarnOnMissingPlatformVersion = true;
                t5.BuildEngine = engine5;
                t5.ProjectName = "project.proj";
                t5.TargetPlatformVersion = "8.1";
                t5.TargetPlatformIdentifier = "windows";
                bool succeeded5 = t5.Execute();

                Assert.True(succeeded5);
                engine5.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionNotSpecified", "project.proj", "BadTestSDK", "1.0", "windows", "8.0", "windows", t5.TargetPlatformVersion);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }
        /// <summary>
        /// Verify "RuntimeReferenceOnly" equals to "true" is set for specified references
        /// </summary>
        [Fact]
        public void VerifyAddMetadataToReferences()
        {
            MockEngine engine = new MockEngine();
            TaskLoggingHelper log = new TaskLoggingHelper(engine, "ResolveSDKReference");
            log.TaskResources = AssemblyResources.PrimaryResources;

            HashSet<SDKReference> references = new HashSet<SDKReference>();
            SDKReference reference1 = new SDKReference(new TaskItem(), "Microsoft.VCLibs", "12.0");
            reference1.ResolvedItem = new TaskItem();
            references.Add(reference1);

            SDKReference reference2 = new SDKReference(new TaskItem(), "Microsoft.VCLibs", "11.0");
            reference2.ResolvedItem = new TaskItem();
            references.Add(reference2);

            SDKReference reference3 = new SDKReference(new TaskItem(), "Foo", "11.0");
            reference3.ResolvedItem = new TaskItem();
            references.Add(reference3);

            // Dictionary with runtime-only dependencies
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("Microsoft.VCLibs", "11.0");

            ResolveSDKReference.AddMetadataToReferences(log, references, dict, "RuntimeReferenceOnly", "true");

            foreach (SDKReference reference in references)
            {
                if (reference.SimpleName.Equals("Microsoft.VCLibs") && reference.Version.Equals("11.0"))
                {
                    Assert.True(reference.ResolvedItem.GetMetadata("RuntimeReferenceOnly").Equals("true"));
                }
                else
                {
                    Assert.False(reference.ResolvedItem.MetadataNames.ToString().Contains("RuntimeReferenceOnly"));
                }
            }
        }

        /// <summary>
        /// Make sure ones which are incorrect and log the correct warning.
        /// </summary>
        [Fact]
        public void VerifyUnResolvedSDKMessage()
        {
            MockEngine engine = new MockEngine();
            TaskLoggingHelper log = new TaskLoggingHelper(engine, "ResolveSDKReference");

            HashSet<SDKReference> references = new HashSet<SDKReference>();

            // All of the dependencies resolve correctly no warnings are expected
            SDKReference reference1 = new SDKReference(new TaskItem(), "reference1", "1.0");
            references.Add(reference1);

            SDKReference reference2 = new SDKReference(new TaskItem(), "reference2", "1.0");
            reference2.DependsOnSDK = "reference1, Version=1.0";
            references.Add(reference2);

            SDKReference reference3 = new SDKReference(new TaskItem(), "reference3", "1.0");
            reference3.DependsOnSDK = "reference1, Version=1.0;reference2, Version=1.0";
            references.Add(reference3);

            SDKReference reference4 = new SDKReference(new TaskItem(), "reference4", "1.0");
            reference4.DependsOnSDK = "reference1, Version=1.0";
            references.Add(reference4);

            SDKReference reference5 = new SDKReference(new TaskItem(), "reference5", "1.0");
            reference5.DependsOnSDK = "reference1, Version=1.0";
            references.Add(reference5);

            ResolveSDKReference.VerifySDKDependsOn(log, references); //, new Version(8, 1), "Windows", null);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Log.Length);

            engine = new MockEngine();
            log = new TaskLoggingHelper(engine, "ResolveSDKReference");
            log.TaskResources = AssemblyResources.PrimaryResources;

            references = new HashSet<SDKReference>();

            reference1 = new SDKReference(new TaskItem(), "reference1", "1.0");
            reference1.DependsOnSDK = "NotThere, Version=1.0";
            references.Add(reference1);

            reference2 = new SDKReference(new TaskItem(), "reference2", "1.0");
            reference2.DependsOnSDK = "reference11, Version=1.0;reference2, Version=1.0;reference77, Version=1.0";
            references.Add(reference2);

            reference3 = new SDKReference(new TaskItem(), "reference3", "1.0");
            reference3.DependsOnSDK = "reference1, Version=1.0;NotThere, Version=1.0;WhereAmI, Version=1.0";
            references.Add(reference3);

            reference4 = new SDKReference(new TaskItem(), "reference4", "1.0");
            reference4.DependsOnSDK = "NotThere, Version=1.0";
            references.Add(reference4);

            ResolveSDKReference.VerifySDKDependsOn(log, references);//, new Version(8, 1), "Windows", null);
            Assert.Equal(4, engine.Warnings);
            Assert.Equal(0, engine.Errors);

            string warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", reference1.SDKName, "\"NotThere, Version=1.0\"");
            engine.AssertLogContains(warning);

            warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", reference2.SDKName, "\"reference11, Version=1.0\", \"reference77, Version=1.0\"");
            engine.AssertLogContains(warning);

            warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", reference3.SDKName, "\"NotThere, Version=1.0\", \"WhereAmI, Version=1.0\"");
            engine.AssertLogContains(warning);

            warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", reference4.SDKName, "\"NotThere, Version=1.0\"");
            engine.AssertLogContains(warning);
        }

        /// <summary>
        /// Verify if the DependsOn metadata is set on the reference item and that dependency is not resolved then cause the warning to happen.
        /// </summary>
        [Fact]
        public void VerifyDependencyWarningFromMetadata()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            item.SetMetadata("DependsOn", "NotHere, Version=1.0");
            t.SDKReferences = new ITaskItem[] { item };
            t.References = null;
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));

            string warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", "GoodTestSDK, Version=2.0", "\"NotHere, Version=1.0\"");
            engine.AssertLogContains(warning);
        }

        /// <summary>
        /// Verify we get the correct dependson warning
        /// </summary>
        [Fact]
        public void VerifyDependsOnWarningFromManifest()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyDependsOnWarningFromManifest");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity = 'ShouldNotPickup'
                FrameworkIdentity-retail = 'ShouldNotPickup'
                FrameworkIdentity-retail-Neutral = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail = 'ShouldNotPickup'
                APPX-Retail-Neutral = 'RetailX86Location'
                SDKType='Debug'
                DependsOn='Foo, Version=1.0;bar, Version=2.0;foooooggg;;;;'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                string warning = ResourceUtilities.FormatResourceString("ResolveSDKReference.SDKMissingDependency", "GoodTestSDK, Version=2.0", "\"Foo, Version=1.0\", \"bar, Version=2.0\"");
                engine.AssertLogContains(warning);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Make sure the equals works on the SDKReference.
        /// </summary>
        [Fact]
        public void TestSDkReferenceEquals()
        {
            ITaskItem dummyItem = new TaskItem();

            SDKReference sdkReference1 = new SDKReference(dummyItem, "Reference1", "8.0");
            SDKReference shouldBeEqualToOne = new SDKReference(dummyItem, "Reference1", "8.0");
            SDKReference sdkReference2 = new SDKReference(dummyItem, "Reference2", "8.0");
            SDKReference sdkReference2DiffVer = new SDKReference(dummyItem, "Reference2", "7.0");

            Assert.True(sdkReference1.Equals(sdkReference1));
            Assert.True(sdkReference1.Equals(shouldBeEqualToOne));
            Assert.False(sdkReference1.Equals(sdkReference2));
            Assert.False(sdkReference1.Equals(sdkReference2DiffVer));
            Assert.False(sdkReference2.Equals(sdkReference2DiffVer));
        }

        private static void TestGoodSDKReferenceIncludes(ITaskItem referenceInclude, string simpleName, string version)
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            t.BuildEngine = engine;

            SDKReference reference = t.ParseSDKReference(referenceInclude);
            Assert.NotNull(reference);
            Assert.True(reference.SimpleName.Equals(simpleName, StringComparison.OrdinalIgnoreCase));
            Assert.True(reference.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        }

        private static void TestBadSDKReferenceIncludes(ITaskItem referenceInclude)
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            t.BuildEngine = engine;

            Assert.Null(t.ParseSDKReference(referenceInclude));
            string errorMessage = t.Log.FormatResourceString("ResolveSDKReference.SDKReferenceIncorrectFormat", referenceInclude.ItemSpec);
            engine.AssertLogContains(errorMessage);
        }


        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:true Target:msil Expect: No error
        /// </summary>
        [Fact]
        public void Prefer32bit1()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit1");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='true'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Errors); // "Expected no errors"
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:false Target:AnyCPU Expect: error
        /// </summary>
        [Fact]
        public void Prefer32bit2()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit2");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='false'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "Any CPU";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
                string errorMessage = t.Log.FormatResourceString("ResolveSDKReference.Prefer32BitNotSupportedWithNeutralProject", item.ItemSpec);
                engine.AssertLogContains(errorMessage);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }


        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:false Target:x86 Expect: No error
        /// </summary>
        [Fact]
        public void Prefer32bit3()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit3");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='false'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "x86";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Errors); // "Expected no errors"
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit false  Manifest:SupportPrefer32Bit:false Target:msil Expect: No error
        /// </summary>
        [Fact]
        public void Prefer32bit4()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit4");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='false'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = false;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Errors); // "Expected no errors"
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit false  Manifest:SupportPrefer32Bit:false Target:x86 Expect: No error
        /// </summary>
        [Fact]
        public void Prefer32bit5()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit5");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='false'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "x86";
                t.Prefer32Bit = false;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Errors); // "Expected no errors"
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:FOO Target:msil Expect: error
        /// </summary>
        [Fact]
        public void Prefer32bit6()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit6");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit='FOO'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings); // "Expected no warnings" ;
                string errorMessage = t.Log.FormatResourceString("ResolveSDKReference.Prefer32BitNotSupportedWithNeutralProject", item.ItemSpec);
                engine.AssertLogContains(errorMessage);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:empty Target:msil Expect: No error
        /// </summary>
        [Fact]
        public void Prefer32bit7()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit7");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                 SupportPrefer32Bit=''>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
                Assert.Equal(0, engine.Errors); // "Expected no errors"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit true  Manifest:SupportPrefer32Bit:missing Target:msil Expect: No Error
        /// </summary>
        [Fact]
        public void Prefer32bit8()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit8");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
                Assert.Equal(0, engine.Errors); // "Expected no errors"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Project: Prefer32bit false  Manifest:SupportPrefer32Bit:true Target:msil Expect: No Error
        /// </summary>
        [Fact]
        public void Prefer32bit9()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "Prefer32bit9");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                SupportPrefer32Bit='true'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKConfiguration = "Release";
                t.TargetedSDKArchitecture = "msil";
                t.Prefer32Bit = true;

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Warnings); // "Expected no warnings"
                Assert.Equal(0, engine.Errors); // "Expected no errors"
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Resolve from an SDK which exists and is not a framework SDK. This means there is no frameworkIdentity or APPXLocation.
        /// Also since no configuration or architecture were passed in we expect the defaults.
        /// </summary>
        [Fact]
        public void ResolveFromNonFrameworkNoManifest()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };
            t.References = null;
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolve from an SDK which exists and is not a framework SDK. This means there is no frameworkIdentity or APPXLocation.
        /// Also since no configuration or architecture were passed in we expect the defaults.
        /// </summary>
        [Fact]
        public void ResolveFromNonFrameworkPassInConfigAndArch()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };
            t.References = null;
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };
            t.TargetedSDKConfiguration = "Release";
            t.TargetedSDKArchitecture = "msil";
            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));

            // Expect retail if release is passed in
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolve from an SDK which exists and is not a framework SDK. This means there is no frameworkIdentity or APPXLocation.
        /// Also since no configuration or architecture were passed in we expect the defaults.
        /// </summary>
        [Fact]
        public void ResolveFromNonFrameworkPassInConfigAndArchOverrideByMetadata()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            item.SetMetadata("TargetedSDKConfiguration", "Release");
            item.SetMetadata("TargetedSDKArchitecture", "AnyCPU");

            t.SDKReferences = new ITaskItem[] { item };
            t.References = null;
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };
            t.TargetedSDKConfiguration = "x86";
            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));

            // Expect retail if release is passed in
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// When duplicate references are passed in we only want the first one.
        /// </summary>
        [Fact]
        public void DuplicateSDKReferences()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            ITaskItem item2 = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item, item2 };
            t.References = new TaskItem[0];
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify that if refernces have SDKName metadata on them that matches a resolved SDK then that SDK should
        /// not have its reference assemblies expanded.
        /// </summary>
        [Fact]
        public void DoNotExpandSDKsWhichAreAlsoTargetedByReferences()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };

            TaskItem referenceItem = new TaskItem("RandomWinMD");
            referenceItem.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.References = new TaskItem[] { referenceItem };

            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SimpleName").Equals("GoodTestSDK", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("Version").Equals("2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));

            // Make sure that if the SDKName does not match the sdk being resolved then it should have no effect.
            // Create the engine.
            engine = new MockEngine();

            t = new ResolveSDKReference();
            item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };

            referenceItem = new TaskItem("RandomWinMD");
            referenceItem.SetMetadata("SDKName", "DifferentSDK, Version=2.0");
            t.References = new TaskItem[] { referenceItem };

            installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// When InstalledSDK is empty we should log a message and succeed.
        /// </summary>
        [Fact]
        public void InstalledSDKEmpty()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };
            t.References = null;
            t.InstalledSDKs = new ITaskItem[0];

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(0, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoSDKLocationsSpecified");
        }

        /// <summary>
        /// Lets have a mix of install sdk items, some are good, some are bad (missing item spec) others are bad (missing SDKName)
        /// </summary>
        [Fact]
        public void MixOfInstalledSDKItemsGoodDuplicateAndBad()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item };
            t.References = new TaskItem[0];

            ITaskItem installedSDK1 = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK1.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

            ITaskItem installedSDK2 = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK2.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

            ITaskItem installedSDK3 = new TaskItem(String.Empty);
            installedSDK3.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

            ITaskItem installedSDK4 = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK4.SetMetadata("SDKName", String.Empty);

            ITaskItem installedSDK5 = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");

            t.InstalledSDKs = new ITaskItem[] { installedSDK1, installedSDK2, installedSDK3, installedSDK4, installedSDK5 };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("SDKType").Length);
            Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("DisplayName").Length);
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKName").Equals("GoodTestSDK, Version=2.0", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Make sure when no sdks are resolved there are no problems and that the names of the sdks which were not resolved are logged.
        /// </summary>
        [Fact]
        public void NOSDKResolved()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            ITaskItem item2 = new TaskItem("GoodTestSDK2, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item, item2 };

            ITaskItem installedSDK = new TaskItem("DoesNotExist");
            installedSDK.SetMetadata("SDKName", "RandomSDK, Version=1.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.False(succeeded);
            Assert.Equal(0, t.ResolvedSDKReferences.Length);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CouldNotResolveSDK", "GoodTestSDK, Version=2.0");
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CouldNotResolveSDK", "GoodTestSDK2, Version=2.0");
        }

        /// <summary>
        /// When there is a mix of resolved and unresolved SDKs make sure that the resolved ones are correctly found
        /// and the unresolved ones are logged.
        /// </summary>
        [Fact]
        public void MixOfResolvedAndUnResolved()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            ITaskItem item2 = new TaskItem("RandomSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[] { item, item2 };

            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            t.LogResolutionErrorsAsWarnings = true;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedSDKReferences.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
            Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(@"c:\SDKDirectory\GoodTestSDK\2.0\", StringComparison.OrdinalIgnoreCase));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.FoundSDK", @"c:\SDKDirectory\GoodTestSDK\2.0\");
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CouldNotResolveSDK", "RandomSDK, Version=2.0");
        }

        /// <summary>
        /// When a null is passed into the SDKReferences property make sure we get the correct exception out.
        /// </summary>
        [Fact]
        public void NullSDKReferences()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                t.SDKReferences = null;
                bool succeeded = t.Execute();
            }
           );
        }
        /// <summary>
        /// When a null is passed into the set of InstalledSDKS property make sure we get the correct exception out.
        /// </summary>
        [Fact]
        public void NullInstalledSDKs()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                t.InstalledSDKs = null;
                bool succeeded = t.Execute();
            }
           );
        }

        /// <summary>
        /// If no SDKReferences are passed in then we should get nothing out.
        /// </summary>
        [Fact]
        public void EmptySDKReferencesList()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ResolveSDKReference t = new ResolveSDKReference();
            ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
            t.SDKReferences = new ITaskItem[0];
            ITaskItem installedSDK = new TaskItem(@"c:\SDKDirectory\GoodTestSDK\2.0\");
            installedSDK.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
            t.InstalledSDKs = new ITaskItem[] { installedSDK };

            t.BuildEngine = engine;
            bool succeeded = t.Execute();

            Assert.True(succeeded);
            Assert.Equal(0, t.ResolvedSDKReferences.Length);
        }

        /// <summary>
        /// When we find the SDKManifest it may be poorly formatted. If that happens we need to log the error 
        /// and not resolve the SDK. We also add a good one as well to make sure resolution continues.
        /// </summary>
        [Fact]
        public void SDKFoundButBadlyFormattedSDKManifestWarnings()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SDKFoundButBadlyFormattedSDKManifestWarnings");
            string testDirectory = Path.Combine(testDirectoryRoot, "BadTestSDK\\2.0\\");
            string sdkManifestContents =
            @"IAMNOTANXMLFILE";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("BadTestSDK, Version=2.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item, item2 };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "BadTestSDK, Version=2.0");

                ITaskItem installLocation2 = new TaskItem("C:\\GoodSDKLocation");
                installLocation2.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2 };
                t.BuildEngine = engine;
                t.LogResolutionErrorsAsWarnings = true;
                bool succeeded = t.Execute();

                Assert.True(succeeded);
                engine.AssertLogContains("MSB3775");

                Assert.Equal(2, t.ResolvedSDKReferences.Length);
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[1].ItemSpec.Equals("C:\\GoodSDKLocation\\", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// When we find the SDKManifest it may be poorly formatted. If that happens we need to log the error 
        /// and not resolve the SDK. We also add a good one as well to make sure resolution continues.
        /// </summary>
        [Fact]
        public void SDKFoundButBadlyFormattedSDKManifestErrors()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SDKFoundButBadlyFormattedSDKManifestErrors");
            string testDirectory = Path.Combine(testDirectoryRoot, "BadTestSDK\\2.0\\");
            string sdkManifestContents =
            @"IAMNOTANXMLFILE";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("BadTestSDK, Version=2.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item, item2 };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "BadTestSDK, Version=2.0");

                ITaskItem installLocation2 = new TaskItem("C:\\GoodSDKLocation");
                installLocation2.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2 };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();

                Assert.False(succeeded);
                engine.AssertLogContains("MSB3775");

                Assert.Equal(1, t.ResolvedSDKReferences.Length);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        [Fact]
        public void TestMaxPlatformVersionWithTargetFrameworkVersion()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "TestMaxPlatformVersionWithTargetFrameworkVersion");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK'
                FrameworkIdentity = ''
                PlatformIdentity = 'Windows'
                APPX = ''
                SDKType=''
                CopyRedistToSubDirectory=''
                SupportedArchitectures=''
                ProductFamilyName=''
                SupportsMultipleVersions=''
                ArchitectureForRuntime = ''
                DependsOn = ''
                MaxPlatformVersion = '6.0'
                MinOSVersion = ''
                MaxOSVersionTested = ''
                MoreInfo='ESDK information'
               >
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string sdkManifestContents2 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK'
                FrameworkIdentity = 'Windows'
                PlatformIdentity = ''
                APPX = ''
                SDKType=''
                CopyRedistToSubDirectory=''
                SupportedArchitectures=''
                ProductFamilyName=''
                SupportsMultipleVersions=''
                ArchitectureForRuntime = ''
                DependsOn = ''
                MaxPlatformVersion = '8.0'
                MinOSVersion = ''
                MaxOSVersionTested = ''
                MoreInfo='ESDK information'
               >
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";


            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

                // In the test below the SDK MaxPlatformVersion is smaller than the TargetPlatformVersion - the build fails
                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                MockEngine engine1 = new MockEngine();
                ResolveSDKReference t1 = new ResolveSDKReference();
                t1.SDKReferences = new ITaskItem[] { item };
                t1.InstalledSDKs = new ITaskItem[] { installLocation };
                t1.TargetPlatformIdentifier = "Windows";
                t1.ProjectName = "myproject.csproj";
                t1.BuildEngine = engine1;
                bool succeeded1 = t1.Execute();

                Assert.True(succeeded1);
                engine1.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionLessThanTargetPlatformVersion", "myproject.csproj", "GoodTestSDK", "2.0", "Windows", "6.0", "Windows", "7.0");
                // In the test below the SDK MaxPlatformVersion is greater than the TargetPlatformVersion - the build succeeds
                File.WriteAllText(sdkManifestFile, sdkManifestContents2);
                MockEngine engine2 = new MockEngine();
                ResolveSDKReference t2 = new ResolveSDKReference();
                t2.SDKReferences = new ITaskItem[] { item };
                t2.InstalledSDKs = new ITaskItem[] { installLocation };
                t1.TargetPlatformIdentifier = "Windows";
                t1.ProjectName = "myproject.csproj";
                t2.BuildEngine = engine1;
                bool succeeded2 = t2.Execute();

                Assert.True(succeeded2);
                engine2.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.MaxPlatformVersionLessThanTargetPlatformVersion", "myproject.csproj", "GoodTestSDK", "2.0", "Windows", "6.0", "Windows", "7.0");
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the manifest attributes are empty.
        /// </summary>
        [Fact]
        public void EmptySDKManifestAttributes()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "EmptySDKManifestAttributes");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = ''
                FrameworkIdentity = ''
                PlatformIdentity = ''
                APPX = ''
                SDKType=''
                CopyRedistToSubDirectory=''
                SupportedArchitectures=''
                ProductFamilyName=''
                SupportsMultipleVersions=''
                ArchitectureForRuntime = ''
                DependsOn = ''
                MaxPlatformVersion = ''
                MinOSVersion = ''
                MaxOSVersionTested = ''
               >
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("SDKType").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("SupportedArchitectures").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("ProductFamilyName").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("DisplayName").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("ArchitectureForRuntime").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("MaxPlatformVersion").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("MaxOSVersionTested").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("MinOSVersion").Length);
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals("Allow", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Length);
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we override ALL of the manifest properties with ones on the metadata
        /// </summary>
        [Fact]
        public void OverrideManifestAttributes()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "OverrideManifestAttributes");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity = 'Manifest Identity'
                PlatformIdentity = 'Manifest platform Identity'
                APPX = 'Manifest Location'
                SDKType='External'
                SupportsMultipleVersions='Warning'
                MaxPlatformVersion = '8.0'
                MinOSVersion = '2.2.1'
                MaxOSVersionTested = '2.2.1'
                CopyRedistToSubDirectory = 'Manifest RedistSubDirectory'
                DependsOn ='Windows SDK, Version 8.0'>

                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />

            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("FrameworkIdentity", "MetadataIdentity");
                item.SetMetadata("PlatformIdentity", "PlatformIdentity");
                item.SetMetadata("AppXLocation", "Metadata AppxLocation");
                item.SetMetadata("SDKType", "External");
                item.SetMetadata("SupportsMultipleVersions", "Error");
                item.SetMetadata("DisplayName", "ManifestDisplayName");
                item.SetMetadata("CopyRedist", "True");
                item.SetMetadata("ExpandReferenceAssemblies", "True");
                item.SetMetadata("CopyLocalExpandedReferenceAssemblies", "True");
                item.SetMetadata("TargetedSDKConfiguration", "Custom");
                item.SetMetadata("TargetedSDKArchitecture", "Any CPU");
                item.SetMetadata("CopyRedistToSubDirectory", "MyRedistSubDirectory");
                item.SetMetadata("MaxPlatformVersion", "9.0");
                item.SetMetadata("MaxOSVersionTested", "3.3.3");
                item.SetMetadata("MinOSVersion", "3.3.3");

                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("MetadataIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Equals("PlatformIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Metadata AppxLocation", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals("Error", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("DisplayName").Equals("ManifestDisplayName", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Custom", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("MyRedistSubDirectory", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("MaxPlatformVersion").Equals("9.0", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("MaxOSVersionTested").Equals("3.3.3", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("MinOSVersion").Equals("3.3.3", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we Have a good manfest that had framework and appx locations that exactly match the targeted sdk configuration and architecture.
        /// </summary>
        [Fact]
        public void GoodManifestMatchingConfigAndArch()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GoodManifestMatchingConfigAndArch");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity = 'ShouldNotPickup'
                FrameworkIdentity-retail = 'ShouldNotPickup'
                FrameworkIdentity-retail-Neutral = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail = 'ShouldNotPickup'
                APPX-Retail-Neutral = 'RetailX86Location'
                SDKType='External'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Neutral|RetailX86Location", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("GoodTestSDK\\Redist", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we Have a good manfest that had framework and appx locations that only match the targeted sdk configuration.
        /// </summary>
        [Fact]
        public void GoodManifestMatchingConfigOnly()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GoodManifestMatchingConfigOnly");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity = 'ShouldNotPickup'
                FrameworkIdentity-Retail = 'GoodTestSDKIdentity'
                FrameworkIdentity-Retail-x64 = 'ShouldNotPickup'
                APPX = 'ShouldNotPickup'
                APPX-Retail = 'RetailNeutralLocation'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Neutral|RetailNeutralLocation", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// TVerify that when a platform identity is found that we do not copy the references or redist
        /// </summary>
        [Fact]
        public void NoCopyOnPlatformIdentityFound()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "NoCopyOnPlatformIdentityFound");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                PlatformIdentity = 'PlatformID'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Length);
                Assert.Equal(0, t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Length);
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Equals("PlatformID", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we Have a good manfest that had framework and appx locations that does not match any of the config arch combinations but does match
        /// and entry name simply FrameworkIdentity or APPX
        /// </summary>
        [Fact]
        public void GoodManifestMatchingBaseNameOnly()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GoodManifestMatchingConfigOnly");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                PlatformIdentity = 'Good Platform'
                FrameworkIdentity = 'GoodTestSDKIdentity'
                FrameworkIdentity-Debug = 'ShouldNotPickup'
                FrameworkIdentity-Debug-x64 = 'ShouldNotPickup'
                APPX = 'Location'
                APPX-Debug = 'ShouldNotPickup'
                APPX-Debug-X64 = 'ShouldNotPickup'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Equals("Good Platform", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Neutral|Location", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("X86", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we only have the arm APPX and it can be found
        /// </summary>
        [Fact]
        public void ManifestOnlyHasArmLocation()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ManifestOnlyHasArmLocation");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                PlatformIdentity = 'Good Platform'
                FrameworkIdentity = 'GoodTestSDKIdentity'
                APPX-ARM = 'ARMAppx'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.TargetedSDKArchitecture = "arm";
                t.TargetedSDKConfiguration = "Debug";
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedSDKReferences.Length);
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Equals("Good Platform", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("arm|ARMAppx", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Debug", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("arm", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where we have a number of locations and arm APPX and can be found
        /// </summary>
        [Fact]
        public void ManifestArmLocationWithOthers()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ManifestArmLocationWithOthers");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                PlatformIdentity = 'Good Platform'
                FrameworkIdentity = 'GoodTestSDKIdentity'
                APPX-ARM = 'ARMAppx'
                APPX-X86 = 'x86Appx'
                APPX-X64 = 'x64Appx'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.TargetedSDKArchitecture = "arm";
                t.TargetedSDKConfiguration = "Debug";
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedSDKReferences.Length);
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("PlatformIdentity").Equals("Good Platform", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("arm|ARMAppx|x64|x64Appx|x86|x86Appx", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Debug", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("arm", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are framework identity attributes but none of the match and there is no base FrameworkIdentity, the 
        /// same is true for APPX.
        /// </summary>
        [Fact]
        public void MatchNoNamesButNamesExistWarning()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "MatchNoNamesButNamesExistWarning");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-Debug = 'ShouldNotPickup'
                FrameworkIdentity-Debug-x64 = 'ShouldNotPickup'
                APPX-Debug = 'ShouldNotPickup'
                APPX-Debug-X64 = 'ShouldNotPickup'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";
                t.BuildEngine = engine;
                t.LogResolutionErrorsAsWarnings = true;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                Assert.Equal(1, t.ResolvedSDKReferences.Length);

                string message = ResourceUtilities.FormatResourceString("ResolveSDKReference.ReadingSDKManifestFile", sdkManifestFile);
                engine.AssertLogContains(message);

                string errorMessage = ResourceUtilities.FormatResourceString("ResolveSDKReference.NoMatchingFrameworkIdentity", sdkManifestFile, "Retail", "x86");
                engine.AssertLogContains(errorMessage);

                errorMessage = ResourceUtilities.FormatResourceString("ResolveSDKReference.NoMatchingAppxLocation", sdkManifestFile, "Retail", "x86");
                engine.AssertLogContains(errorMessage);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are framework identity attributes but none of the match and there is no base FrameworkIdentity, the 
        /// same is true for APPX.
        /// </summary>
        [Fact]
        public void MatchNoNamesButNamesExistError()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "MatchNoNamesButNamesExistError");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-Debug = 'ShouldNotPickup'
                FrameworkIdentity-Debug-x64 = 'ShouldNotPickup'
                APPX-Debug = 'ShouldNotPickup'
                APPX-Debug-X64 = 'ShouldNotPickup'
                SDKType='External'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";
                t.LogResolutionErrorsAsWarnings = false;
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                Assert.Equal(0, t.ResolvedSDKReferences.Length);

                string errorMessage = ResourceUtilities.FormatResourceString("ResolveSDKReference.NoMatchingFrameworkIdentity", sdkManifestFile, "Retail", "x86");
                engine.AssertLogContains(errorMessage);

                errorMessage = ResourceUtilities.FormatResourceString("ResolveSDKReference.NoMatchingAppxLocation", sdkManifestFile, "Retail", "x86");
                engine.AssertLogContains(errorMessage);
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }


        /// <summary>
        /// Test the case where there is a single supported architecture and the project targets that architecture
        /// </summary>
        [Fact]
        public void SingleSupportedArchitectureMatchesProject()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SingleSupportedArchitectureMatchesProject");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("x64|RetailX64Location|x86|RetailX86Location", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("X86", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("GoodTestSDK\\Redist", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the productfamily is set in the manifest and not as metadata on the reference item.
        /// </summary>
        [Fact]
        public void ProductFamilySetInManifest()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ProductFamilySetInManifest");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                ProductFamilyName = 'MyFamily'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ProductFamilyName").Equals("MyFamily", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the productfamily is set in the manifest and as metadata on the reference item. Expect the metadata to win.
        /// </summary>
        [Fact]
        public void ProductFamilySetInManifestAndMetadata()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ProductFamilySetInManifestAndMetadata");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                ProductFamilyName = 'MyFamily'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("ProductFamilyName", "MetadataFamily");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ProductFamilyName").Equals("MetadataFamily", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the SupportsMultipleVersions is NOT in the manifest or on metadata
        /// </summary>
        [Fact]
        public void SupportsMultipleVersionsNotInManifest()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SupportsMultipleVersionsNotInManifest");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                ProductFamilyName = 'MyFamily'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);
                Assert.Equal(0, engine.Warnings);
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals("Allow", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where metadata on the item is bad, we should then read from the manifest.
        /// </summary>
        [Fact]
        public void SupportsMultipleVersionsBadMetadata()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SupportsMultipleVersionsBadMetadata");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Warning'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("SupportsMultipleVersions", "WoofWoof");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals("Warning", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are conflicts between sdks of the same product family
        /// </summary>
        [Fact]
        public void ConflictsBetweenSameProductFamilySameName()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ConflictsBetweenSameProductFamilySameName");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\1.0\\");
            string testDirectory2 = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string testDirectory3 = Path.Combine(testDirectoryRoot, "GoodTestSDK\\3.0\\");

            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=1.0'
                DisplayName = 'GoodTestSDK 1.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Warning'>
            </FileList>";

            string sdkManifestContents2 =
           @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Error'>
            </FileList>";

            string sdkManifestContents3 =
           @"<FileList
                Identity = 'GoodTestSDK, Version=3.0'
                DisplayName = 'GoodTestSDK 3.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Allow'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(testDirectory2, "SdkManifest.xml");
                string sdkManifestFile3 = Path.Combine(testDirectory3, "SdkManifest.xml");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(testDirectory2);
                Directory.CreateDirectory(testDirectory3);

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(sdkManifestFile3, sdkManifestContents3);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=1.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK, Version=2.0");
                ITaskItem item3 = new TaskItem("GoodTestSDK, Version=3.0");

                t.SDKReferences = new ITaskItem[] { item, item2, item3 };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=1.0");

                ITaskItem installLocation2 = new TaskItem(testDirectory2);
                installLocation2.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

                ITaskItem installLocation3 = new TaskItem(testDirectory3);
                installLocation3.SetMetadata("SDKName", "GoodTestSDK, Version=3.0");

                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2, installLocation3 };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", "GoodTestSDK, Version=1.0", "\"GoodTestSDK, Version=2.0\", \"GoodTestSDK, Version=3.0\"", "MyFamily");
                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", "GoodTestSDK, Version=2.0", "\"GoodTestSDK, Version=1.0\", \"GoodTestSDK, Version=3.0\"", "MyFamily");
                Assert.Equal(1, engine.Warnings);
                Assert.Equal(1, engine.Errors);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[1].ItemSpec.Equals(testDirectory2, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[2].ItemSpec.Equals(testDirectory3, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are conflicts between sdks of the same product family
        /// </summary>
        [Fact]
        public void ConflictsBetweenSameProductFamilyDiffName()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ConflictsBetweenSameProductFamilyDiffName");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\1.0\\");
            string testDirectory2 = Path.Combine(testDirectoryRoot, "GoodTestSDK1\\2.0\\");
            string testDirectory3 = Path.Combine(testDirectoryRoot, "GoodTestSDK3\\3.0\\");

            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=1.0'
                DisplayName = 'GoodTestSDK 1.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Warning'>
            </FileList>";

            string sdkManifestContents2 =
           @"<FileList
                Identity = 'GoodTestSDK2, Version=2.0'
                DisplayName = 'GoodTestSDK2 2.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Error'>
            </FileList>";

            string sdkManifestContents3 =
           @"<FileList
                Identity = 'GoodTestSDK3, Version=3.0'
                DisplayName = 'GoodTestSDK3 3.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Allow'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(testDirectory2, "SdkManifest.xml");
                string sdkManifestFile3 = Path.Combine(testDirectory3, "SdkManifest.xml");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(testDirectory2);
                Directory.CreateDirectory(testDirectory3);

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(sdkManifestFile3, sdkManifestContents3);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=1.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK2, Version=2.0");
                ITaskItem item3 = new TaskItem("GoodTestSDK3, Version=3.0");

                t.SDKReferences = new ITaskItem[] { item, item2, item3 };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=1.0");

                ITaskItem installLocation2 = new TaskItem(testDirectory2);
                installLocation2.SetMetadata("SDKName", "GoodTestSDK2, Version=2.0");

                ITaskItem installLocation3 = new TaskItem(testDirectory3);
                installLocation3.SetMetadata("SDKName", "GoodTestSDK3, Version=3.0");

                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2, installLocation3 };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", "GoodTestSDK, Version=1.0", "\"GoodTestSDK2, Version=2.0\", \"GoodTestSDK3, Version=3.0\"", "MyFamily");
                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", "GoodTestSDK2, Version=2.0", "\"GoodTestSDK, Version=1.0\", \"GoodTestSDK3, Version=3.0\"", "MyFamily");
                Assert.Equal(1, engine.Warnings);
                Assert.Equal(1, engine.Errors);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[1].ItemSpec.Equals(testDirectory2, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[2].ItemSpec.Equals(testDirectory3, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are conflicts between sdks of the same product family
        /// </summary>
        [Fact]
        public void ConflictsBetweenMIXPFAndName()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ConflictsBetweenSameProductFamilyDiffName");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\1.0\\");
            string testDirectory2 = Path.Combine(testDirectoryRoot, "GoodTestSDK2\\2.0\\");
            string testDirectory3 = Path.Combine(testDirectoryRoot, "GoodTestSDK3\\3.0\\");
            string testDirectory4 = Path.Combine(testDirectoryRoot, "GoodTestSDK3\\4.0\\");

            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=1.0'
                DisplayName = 'GoodTestSDK 1.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Warning'>
            </FileList>";

            string sdkManifestContents2 =
           @"<FileList
                Identity = 'GoodTestSDK2, Version=2.0'
                DisplayName = 'GoodTestSDK2 2.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Allow'>
            </FileList>";

            string sdkManifestContents3 =
           @"<FileList
                Identity = 'GoodTestSDK3, Version=3.0'
                DisplayName = 'GoodTestSDK3 3.0'
                SupportsMultipleVersions='Warning'>
            </FileList>";


            string sdkManifestContents4 =
           @"<FileList
                Identity = 'GoodTestSDK3, Version=4.0'
                DisplayName = 'GoodTestSDK3 4.0'
                SupportsMultipleVersions='Allow'>
            </FileList>";
            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(testDirectory2, "SdkManifest.xml");
                string sdkManifestFile3 = Path.Combine(testDirectory3, "SdkManifest.xml");
                string sdkManifestFile4 = Path.Combine(testDirectory4, "SdkManifest.xml");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(testDirectory2);
                Directory.CreateDirectory(testDirectory3);
                Directory.CreateDirectory(testDirectory4);

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(sdkManifestFile3, sdkManifestContents3);
                File.WriteAllText(sdkManifestFile4, sdkManifestContents4);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=1.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK2, Version=2.0");
                ITaskItem item3 = new TaskItem("GoodTestSDK3, Version=3.0");
                ITaskItem item4 = new TaskItem("GoodTestSDK3, Version=4.0");

                t.SDKReferences = new ITaskItem[] { item, item2, item3, item4 };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=1.0");

                ITaskItem installLocation2 = new TaskItem(testDirectory2);
                installLocation2.SetMetadata("SDKName", "GoodTestSDK2, Version=2.0");

                ITaskItem installLocation3 = new TaskItem(testDirectory3);
                installLocation3.SetMetadata("SDKName", "GoodTestSDK3, Version=3.0");

                ITaskItem installLocation4 = new TaskItem(testDirectory4);
                installLocation4.SetMetadata("SDKName", "GoodTestSDK3, Version=4.0");

                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2, installLocation3, installLocation4 };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameFamily", "GoodTestSDK, Version=1.0", "\"GoodTestSDK2, Version=2.0\"", "MyFamily");
                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameName", "GoodTestSDK3, Version=3.0", "\"GoodTestSDK3, Version=4.0\"");
                Assert.Equal(2, engine.Warnings);
                Assert.Equal(0, engine.Errors);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[1].ItemSpec.Equals(testDirectory2, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[2].ItemSpec.Equals(testDirectory3, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[3].ItemSpec.Equals(testDirectory4, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there are conflicts between sdks of the same SDK Name
        /// </summary>
        [Fact]
        public void ConflictsBetweenSameSDKName()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "ConflictsBetweenSameSDKName");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\1.0\\");
            string testDirectory2 = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string testDirectory3 = Path.Combine(testDirectoryRoot, "GoodTestSDK\\3.0\\");

            string sdkManifestContents1 =
            @"<FileList
                Identity = 'GoodTestSDK, Version=1.0'
                DisplayName = 'GoodTestSDK 1.0'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='Warning'>
            </FileList>";

            string sdkManifestContents2 =
           @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                ProductFamilyName = 'MyFamily2'
                SupportsMultipleVersions='Error'>
            </FileList>";

            string sdkManifestContents3 =
           @"<FileList
                Identity = 'GoodTestSDK, Version=3.0'
                DisplayName = 'GoodTestSDK 3.0'
                ProductFamilyName = 'MyFamily3'
                SupportsMultipleVersions='Allow'>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(testDirectory2, "SdkManifest.xml");
                string sdkManifestFile3 = Path.Combine(testDirectory3, "SdkManifest.xml");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);
                Directory.CreateDirectory(testDirectory2);
                Directory.CreateDirectory(testDirectory3);

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(sdkManifestFile3, sdkManifestContents3);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=1.0");
                ITaskItem item2 = new TaskItem("GoodTestSDK, Version=2.0");
                ITaskItem item3 = new TaskItem("GoodTestSDK, Version=3.0");

                t.SDKReferences = new ITaskItem[] { item, item2, item3 };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=1.0");

                ITaskItem installLocation2 = new TaskItem(testDirectory2);
                installLocation2.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");

                ITaskItem installLocation3 = new TaskItem(testDirectory3);
                installLocation3.SetMetadata("SDKName", "GoodTestSDK, Version=3.0");

                t.InstalledSDKs = new ITaskItem[] { installLocation, installLocation2, installLocation3 };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameName", "GoodTestSDK, Version=1.0", "\"GoodTestSDK, Version=2.0\", \"GoodTestSDK, Version=3.0\"");
                engine.AssertLogContainsMessageFromResource(_resourceDelegate, "ResolveSDKReference.CannotReferenceTwoSDKsSameName", "GoodTestSDK, Version=2.0", "\"GoodTestSDK, Version=1.0\", \"GoodTestSDK, Version=3.0\"");
                Assert.Equal(1, engine.Warnings);
                Assert.Equal(1, engine.Errors);

                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[1].ItemSpec.Equals(testDirectory2, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[2].ItemSpec.Equals(testDirectory3, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where metadata on the item is bad, we should then read from the manifest.
        /// </summary>
        [Fact]
        public void SupportsMultipleVersionsReadManifest()
        {
            SupportsMultipleVersionsVerifyManifestReading("Error");
            SupportsMultipleVersionsVerifyManifestReading("Warning");
            SupportsMultipleVersionsVerifyManifestReading("Allow");
            SupportsMultipleVersionsVerifyManifestReading("WoofWoof");
        }

        private void SupportsMultipleVersionsVerifyManifestReading(string manifestEntry)
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SupportsMultipleVersionsVerifyManifestReading");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                ProductFamilyName = 'MyFamily'
                SupportsMultipleVersions='" + manifestEntry + @"'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "x86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                if (String.Equals(manifestEntry, "WoofWoof", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals("Allow", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SupportsMultipleVersions").Equals(manifestEntry, StringComparison.OrdinalIgnoreCase));
                }
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the supportedArchitectures are empty
        /// </summary>
        [Fact]
        public void EmptyArchitectures()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "OverrideManifestWithMetadata");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX = 'NeutralLocation'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                APPX-Retail-Arm = 'RetailArmLocation'
                SDKType='External'
                SupportedArchitectures = ';  ;  ;  ;'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("SupportedArchitectures", "X86");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "X86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Arm|RetailArmLocation|Neutral|NeutralLocation|X64|RetailX64Location|X86|RetailX86Location", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("X86", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("GoodTestSDK\\Redist", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the metadata on the reference overrides what is in the manifest but it does not match what is being targeted
        /// </summary>
        [Fact]
        public void OverrideManifestWithMetadataButMetadataDoesNotMatch()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "OverrideManifestWithMetadataButMetadataDoesNotMatch");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'X86'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("SupportedArchitectures", "ARM");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "X86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                Assert.Equal(0, t.ResolvedSDKReferences.Length);
                engine.AssertLogContains("MSB3779");
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the metadata on the reference overrides what is in the manifest
        /// </summary>
        [Fact]
        public void OverrideManifestWithMetadata()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "OverrideManifestWithMetadata");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX = 'NeutralLocation'
                APPX-Debug-x86 = 'DebugX86Location'
                APPX-Debug-x64 = 'DebugX64Location'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'ARM'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                item.SetMetadata("SupportedArchitectures", "X64;X86;Neutral");

                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "X86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Neutral|NeutralLocation|X64|RetailX64Location|X86|RetailX86Location", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("X86", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("GoodTestSDK\\Redist", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is a single supported architecture and the project does not target that architecture
        /// </summary>
        [Fact]
        public void SingleSupportedArchitectureDoesNotMatchProject()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "SingleSupportedArchitectureDoesNotMatchProject");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-x86 = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail-x86 = 'RetailX86Location'
                APPX-Retail-x64 = 'RetailX64Location'
                SDKType='External'
                SupportedArchitectures = 'ARM'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "X86";
                t.TargetedSDKConfiguration = "Release";

                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                Assert.Equal(0, t.ResolvedSDKReferences.Length);
                engine.AssertLogContains("MSB3779");
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is are multiple supported architecture and the project targets one of those architectures
        /// </summary>
        [Fact]
        public void MultipleSupportedArchitectureMatchesProject()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "MultipleSupportedArchitectureMatchesProject");
            string testDirectory = Path.Combine(testDirectoryRoot, "GoodTestSDK\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-Neutral = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail-Neutral = 'RetailNeutralLocation'
                SDKType='External'
                SupportedArchitectures = 'X86;Neutral;X64'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "AnyCPU";
                t.TargetedSDKConfiguration = "Release";
                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.True(succeeded);

                engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "ResolveSDKReference.NoFrameworkIdentitiesFound");
                Assert.True(t.ResolvedSDKReferences[0].ItemSpec.Equals(testDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("FrameworkIdentity").Equals("GoodTestSDKIdentity", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("AppXLocation").Equals("Neutral|RetailNeutralLocation", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("SDKType").Equals("External", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedist").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("ExpandReferenceAssemblies").Equals("True", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyLocalExpandedReferenceAssemblies").Equals("False", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("OriginalItemSpec").Equals(item.ItemSpec, StringComparison.OrdinalIgnoreCase));
                Assert.True(t.ResolvedSDKReferences[0].GetMetadata("CopyRedistToSubDirectory").Equals("GoodTestSDK\\Redist", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is are multiple supported architecture and the project does not match one of those architectures
        /// </summary>
        [Fact]
        public void MultipleSupportedArchitectureDoesNotMatchProject()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "MultipleSupportedArchitectureMatchesProject");
            string testDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string sdkManifestContents =
            @"<FileList
                Identity = 'GoodTestSDK, Version=2.0'
                DisplayName = 'GoodTestSDK 2.0'
                FrameworkIdentity-retail-Neutral = 'GoodTestSDKIdentity'
                APPX = 'ShouldNotPickup'
                APPX-Retail-Neutral = 'RetailNeutralLocation'
                SDKType='External'
                SupportedArchitectures = 'X86;Neutral;X64'
                CopyRedistToSubDirectory='GoodTestSDK\Redist'>
                <File WinMD = 'GoodTestSDK.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            try
            {
                string sdkManifestFile = Path.Combine(testDirectory, "SdkManifest.xml");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectory);

                File.WriteAllText(sdkManifestFile, sdkManifestContents);

                // Create the engine.
                MockEngine engine = new MockEngine();

                ResolveSDKReference t = new ResolveSDKReference();
                ITaskItem item = new TaskItem("GoodTestSDK, Version=2.0");
                t.SDKReferences = new ITaskItem[] { item };
                t.TargetedSDKArchitecture = "ARM";
                t.TargetedSDKConfiguration = "Release";
                ITaskItem installLocation = new TaskItem(testDirectory);
                installLocation.SetMetadata("SDKName", "GoodTestSDK, Version=2.0");
                t.InstalledSDKs = new ITaskItem[] { installLocation };
                t.BuildEngine = engine;
                bool succeeded = t.Execute();
                Assert.False(succeeded);

                Assert.Equal(0, t.ResolvedSDKReferences.Length);
                engine.AssertLogContains("MSB3779");
            }
            finally
            {
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }
        #endregion
    }

    /// <summary>
    /// Test the output groups which will be used to generate the recipe fileGatherSDKOutputGroups
    /// </summary>
    public class GatherSDKOutputGroupsTestFixture
    {
        [Fact]
        public void GatherSDKOutputGroupsTargetArchitectureExists()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GatherSDKOutputGroupsWithFramework");
            string sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string archRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\x86");
            string neutralRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string archCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\x86");
            string neutralCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");

            string sdkDirectory3 = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\FrameworkSDkWithManifest\\2.0\\");
            string archRedist3 = Path.Combine(sdkDirectory3, "Redist\\Retail\\x64");
            string archRedist33 = Path.Combine(sdkDirectory3, "Redist\\Retail\\Neutral");
            string archCommonRedist3 = Path.Combine(sdkDirectory3, "Redist\\CommonConfiguration\\x64");

            string sdkManifestContents =
            @"<FileList
                Identity = 'SDkWithManifest, Version=2.0'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'SDkWithManifest 2.0'
                CopyRedistToSubDirectory='SomeOtherRedistDirectory'>
                <File WinMD = 'SDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string sdkManifestContents2 =
          @"<FileList
                Identity = 'FrameworkSDkWithManifest, Version=2.0'
                FrameworkIdentity='FrameworkSDK'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'AnotherSDkWithManifest 2.0'
                CopyRedistToSubDirectory='SomeOtherRedistDirectory'> 
                <File WinMD = 'AnotherSDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(
             @"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <ItemGroup>
                  <SDKReference Include=""SDkWithManifest, Version=2.0"">
                      <TargetedSDKConfiguration>Release</TargetedSDKConfiguration>
                      <CopyRedistToSubDirectory>AnotherRedistLocation</CopyRedistToSubDirectory>
                   </SDKReference>
                <SDKReference Include=""FrameworkSDKWithManifest, Version=2.0"">
                      <TargetedSDKArchitecture>amd64</TargetedSDKArchitecture>
                      <CopyRedistToSubDirectory>AnotherRedistLocation</CopyRedistToSubDirectory>
                   </SDKReference>
                 </ItemGroup>
                 <PropertyGroup>
                     <Configuration>CAT</Configuration>" +
                    @"<OutputPath>" + testDirectoryRoot + "</OutputPath>" +
                    @"<TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                Directory.CreateDirectory(archRedist);
                Directory.CreateDirectory(neutralRedist);
                Directory.CreateDirectory(archCommonRedist);
                Directory.CreateDirectory(neutralCommonRedist);

                Directory.CreateDirectory(sdkDirectory3);
                Directory.CreateDirectory(archRedist3);
                Directory.CreateDirectory(archRedist33);
                Directory.CreateDirectory(archCommonRedist3);

                string sdkManifestFile = Path.Combine(sdkDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(sdkDirectory3, "SdkManifest.xml");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(sdkManifestFile, sdkManifestContents);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(testProjectFile, tempProjectContents);


                string redist1 = Path.Combine(archRedist, "A.dll");
                string redist2 = Path.Combine(neutralRedist, "B.dll");
                string redist3 = Path.Combine(archCommonRedist, "C.dll");
                string redist4 = Path.Combine(neutralCommonRedist, "D.dll");
                string redist5 = Path.Combine(archRedist33, "A.dll");
                string redist6 = Path.Combine(archCommonRedist3, "B.dll");

                File.WriteAllText(redist1, "Test");
                File.WriteAllText(redist2, "Test");
                File.WriteAllText(redist3, "Test");
                File.WriteAllText(redist4, "Test");
                File.WriteAllText(redist5, "Test");
                File.WriteAllText(redist6, "Test");

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                ProjectInstance project = pc.LoadProject(testProjectFile).CreateProjectInstance();
                project.SetProperty("SDKReferenceDirectoryRoot", testDirectoryRoot);
                project.SetProperty("SDKReferenceRegistryRoot", "");

                IDictionary<string, TargetResult> targetResults = new Dictionary<string, TargetResult>();
                bool success = project.Build(new string[] { "GetResolvedSDKReferences" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("GetResolvedSDKReferences"));
                TargetResult result = targetResults["GetResolvedSDKReferences"];
                ITaskItem[] resolvedSDKReferences = result.Items;
                Assert.Equal(2, resolvedSDKReferences.Length);

                logger = new MockLogger();
                targetResults = new Dictionary<string, TargetResult>();
                success = project.Build(new string[] { "SDKRedistOutputGroup" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("SDKRedistOutputGroup"));
                result = targetResults["SDKRedistOutputGroup"];
                ITaskItem[] SDkRedistFolders = result.Items;
                Assert.Equal(2, SDkRedistFolders.Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        [Fact]
        public void GatherSDKOutputGroupsTargetArchitectureExists2()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GatherSDKOutputGroupsWithFramework");
            string sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string archRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\x86");
            string neutralRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string archCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\x86");
            string neutralCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");

            string sdkDirectory3 = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\FrameworkSDkWithManifest\\2.0\\");
            string archRedist3 = Path.Combine(sdkDirectory3, "Redist\\Retail\\x64");
            string archRedist33 = Path.Combine(sdkDirectory3, "Redist\\Retail\\Neutral");
            string archCommonRedist3 = Path.Combine(sdkDirectory3, "Redist\\CommonConfiguration\\x64");

            string sdkManifestContents =
            @"<FileList
                Identity = 'SDkWithManifest, Version=2.0'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'SDkWithManifest 2.0'
                CopyRedistToSubDirectory='SomeOtherRedistDirectory'>
                <File WinMD = 'SDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string sdkManifestContents2 =
          @"<FileList
                Identity = 'FrameworkSDkWithManifest, Version=2.0'
                FrameworkIdentity='FrameworkSDK'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'AnotherSDkWithManifest 2.0'
                CopyRedistToSubDirectory='SomeOtherRedistDirectory'>
                <File WinMD = 'AnotherSDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(
             @"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <ItemGroup>
                  <SDKReference Include=""SDkWithManifest, Version=2.0"">
                      <TargetedSDKConfiguration>Release</TargetedSDKConfiguration>
                   </SDKReference>
                <SDKReference Include=""FrameworkSDKWithManifest, Version=2.0"">
                      <TargetedSDKArchitecture>amd64</TargetedSDKArchitecture>
                   </SDKReference>
                 </ItemGroup>
                 <PropertyGroup>
                     <Configuration>CAT</Configuration>" +
                    @"<OutputPath>" + testDirectoryRoot + "</OutputPath>" +
                    @"<TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                Directory.CreateDirectory(archRedist);
                Directory.CreateDirectory(neutralRedist);
                Directory.CreateDirectory(archCommonRedist);
                Directory.CreateDirectory(neutralCommonRedist);

                Directory.CreateDirectory(sdkDirectory3);
                Directory.CreateDirectory(archRedist3);
                Directory.CreateDirectory(archRedist33);
                Directory.CreateDirectory(archCommonRedist3);

                string sdkManifestFile = Path.Combine(sdkDirectory, "SdkManifest.xml");
                string sdkManifestFile2 = Path.Combine(sdkDirectory3, "SdkManifest.xml");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(sdkManifestFile, sdkManifestContents);
                File.WriteAllText(sdkManifestFile2, sdkManifestContents2);
                File.WriteAllText(testProjectFile, tempProjectContents);


                string redist1 = Path.Combine(archRedist, "A.dll");
                string redist2 = Path.Combine(neutralRedist, "B.dll");
                string redist3 = Path.Combine(archCommonRedist, "C.dll");
                string redist4 = Path.Combine(neutralCommonRedist, "D.dll");
                string redist5 = Path.Combine(archRedist3, "D.dll");
                string redist6 = Path.Combine(archRedist33, "A.dll");
                string redist7 = Path.Combine(archCommonRedist3, "B.dll");

                File.WriteAllText(redist1, "Test");
                File.WriteAllText(redist2, "Test");
                File.WriteAllText(redist3, "Test");
                File.WriteAllText(redist4, "Test");
                File.WriteAllText(redist5, "Test");
                File.WriteAllText(redist6, "Test");
                File.WriteAllText(redist7, "Test");

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                ProjectInstance project = pc.LoadProject(testProjectFile).CreateProjectInstance();
                project.SetProperty("SDKReferenceDirectoryRoot", testDirectoryRoot);
                project.SetProperty("SDKReferenceRegistryRoot", "");

                IDictionary<string, TargetResult> targetResults = new Dictionary<string, TargetResult>();
                bool success = project.Build(new string[] { "GetResolvedSDKReferences" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("GetResolvedSDKReferences"));
                TargetResult result = targetResults["GetResolvedSDKReferences"];
                ITaskItem[] resolvedSDKReferences = result.Items;
                Assert.Equal(2, resolvedSDKReferences.Length);

                logger = new MockLogger();
                targetResults = new Dictionary<string, TargetResult>();
                success = project.Build(new string[] { "SDKRedistOutputGroup" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("SDKRedistOutputGroup"));
                result = targetResults["SDKRedistOutputGroup"];
                ITaskItem[] SDkRedistFolders = result.Items;
                Assert.Equal(2, SDkRedistFolders.Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }


        [Fact]
        public void GatherSDKOutputGroupsTargetArchitectureDoesNotExists()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "GatherSDKOutputGroupsTargetArchitectureDoesNotExists");
            string sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string x86Redist = Path.Combine(sdkDirectory, "Redist\\Retail\\x86");
            string neutralRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string x86CommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\x86");
            string neutralCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");

            string sdkManifestContents =
            @"<FileList
                Identity = 'SDkWithManifest, Version=2.0'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'SDkWithManifest 2.0'>
                <File WinMD = 'SDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(
             @"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <ItemGroup>
                  <SDKReference Include=""SDkWithManifest, Version=2.0"">
                      <TargetedSDKConfiguration>Release</TargetedSDKConfiguration>
                   </SDKReference>
                 </ItemGroup>
                 <PropertyGroup>
                      <Configuration>CAT</Configuration>" +
                    @"<OutputPath>" + testDirectoryRoot + "</OutputPath>" +
                    @"<TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                Directory.CreateDirectory(x86Redist);
                Directory.CreateDirectory(x86CommonRedist);
                Directory.CreateDirectory(neutralRedist);
                Directory.CreateDirectory(neutralCommonRedist);

                string sdkManifestFile = Path.Combine(sdkDirectory, "SdkManifest.xml");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(sdkManifestFile, sdkManifestContents);
                File.WriteAllText(testProjectFile, tempProjectContents);


                string redist1 = Path.Combine(x86CommonRedist, "A.dll");
                string redist2 = Path.Combine(x86Redist, "B.dll");
                string redist3 = Path.Combine(neutralRedist, "C.dll");
                string redist4 = Path.Combine(neutralCommonRedist, "D.dll");

                File.WriteAllText(redist1, "Test");
                File.WriteAllText(redist2, "Test");
                File.WriteAllText(redist3, "Test");
                File.WriteAllText(redist4, "Test");

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                ProjectInstance project = pc.LoadProject(testProjectFile).CreateProjectInstance();
                project.SetProperty("SDKReferenceDirectoryRoot", testDirectoryRoot);
                project.SetProperty("SDKReferenceRegistryRoot", "");

                IDictionary<string, TargetResult> targetResults = new Dictionary<string, TargetResult>();
                bool success = project.Build(new string[] { "GetResolvedSDKReferences" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("GetResolvedSDKReferences"));
                TargetResult result = targetResults["GetResolvedSDKReferences"];
                ITaskItem[] resolvedSDKReferences = result.Items;
                Assert.Equal(1, resolvedSDKReferences.Length);

                logger = new MockLogger();
                targetResults = new Dictionary<string, TargetResult>();
                success = project.Build(new string[] { "SDKRedistOutputGroup" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("SDKRedistOutputGroup"));
                result = targetResults["SDKRedistOutputGroup"];
                ITaskItem[] SDkRedistFolders = result.Items;
                Assert.Equal(2, SDkRedistFolders.Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);


                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        [Fact]
        public void CheckDefaultingOfTargetConfigAndArchitecture()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "CheckDefaultingOfTargetConfigAndArchitecture");
            string sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string neutralRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string neutralCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");

            string sdkManifestContents =
            @"<FileList
                Identity = 'SDkWithManifest, Version=2.0'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'SDkWithManifest 2.0'>
                <File WinMD = 'SDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(
             @"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <ItemGroup>
                  <SDKReference Include=""SDkWithManifest, Version=2.0""/>
                 </ItemGroup>
                 <PropertyGroup>
                    <Configuration>CAT</Configuration>" +
                    @"<OutputPath>" + testDirectoryRoot + "</OutputPath>" +
                    @"<TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                Directory.CreateDirectory(neutralRedist);
                Directory.CreateDirectory(neutralCommonRedist);

                string sdkManifestFile = Path.Combine(sdkDirectory, "SdkManifest.xml");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(sdkManifestFile, sdkManifestContents);
                File.WriteAllText(testProjectFile, tempProjectContents);

                string redist1 = Path.Combine(neutralRedist, "B.dll");
                string redist2 = Path.Combine(neutralCommonRedist, "C.dll");

                File.WriteAllText(redist1, "Test");
                File.WriteAllText(redist2, "Test");

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                ProjectInstance project = pc.LoadProject(testProjectFile).CreateProjectInstance();
                project.SetProperty("SDKReferenceDirectoryRoot", testDirectoryRoot);
                project.SetProperty("SDKReferenceRegistryRoot", "");

                IDictionary<string, TargetResult> targetResults = new Dictionary<string, TargetResult>();
                bool success = project.Build(new string[] { "GetResolvedSDKReferences" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("GetResolvedSDKReferences"));
                TargetResult result = targetResults["GetResolvedSDKReferences"];
                ITaskItem[] resolvedSDKReferences = result.Items;
                Assert.Equal(1, resolvedSDKReferences.Length);
                Assert.True(resolvedSDKReferences[0].GetMetadata("TargetedSDKConfiguration").Equals("Retail", StringComparison.OrdinalIgnoreCase));
                Assert.True(resolvedSDKReferences[0].GetMetadata("TargetedSDKArchitecture").Equals("Neutral", StringComparison.OrdinalIgnoreCase));

                logger = new MockLogger();
                targetResults = new Dictionary<string, TargetResult>();
                success = project.Build(new string[] { "SDKRedistOutputGroup" }, new ILogger[] { logger }, out targetResults);
                Assert.True(success);
                Assert.True(targetResults.ContainsKey("SDKRedistOutputGroup"));
                result = targetResults["SDKRedistOutputGroup"];
                ITaskItem[] SDkRedistFolders = result.Items;
                Assert.Equal(2, SDkRedistFolders.Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        [Fact]
        public void CheckAttributesFromManifestArePassedToResolvedAssemblies()
        {
            /* \Microsoft SDKs\Windows\v8.0\ExtensionSDKs */
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "CheckDefaultingOfTargetConfigAndArchitecture");
            string sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\v8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string neutralRedist = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string neutralCommonRedist = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");

            string sdkManifestContents1 =
            @"<FileList
                Identity = 'SDkWithManifest, Version=2.0'
                APPX ='AppxLocation'
                SDKType ='External'
                DisplayName = 'SDkWithManifest 2.0'
                MoreInfo = 'http://msdn.microsoft.com/MySDK'
                MaxPlatformVersion = '9.0'
                MinOSVersion = '6.2.0'
                MaxOSVersionTested = '6.2.3'>
                <File WinMD = 'SDkWithManifest.Sprint, Version=8.0' />
                <File AssemblyName = 'Assembly1, Version=8.0' />
                <DependsOn Identity='Windows SDK, Version 8.0'/>
            </FileList>";

            // This is not a framework SDK because it does not have FrameworkIdentity set
            string sdkManifestContents2 = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";



            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <ItemGroup>
                  <SDKReference Include=""SDkWithManifest, Version=2.0""/>
                 </ItemGroup>
                 <PropertyGroup>
                    <Configuration>CAT</Configuration>" +
                    @"<OutputPath>" + testDirectoryRoot + "</OutputPath>" +
                    @"<TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                Directory.CreateDirectory(neutralRedist);
                Directory.CreateDirectory(neutralCommonRedist);

                string redist1 = Path.Combine(neutralRedist, "B.dll");
                string redist2 = Path.Combine(neutralCommonRedist, "C.dll");

                File.WriteAllText(redist1, "Test");
                File.WriteAllText(redist2, "Test");

                MockLogger logger = new MockLogger();

                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");
                File.WriteAllText(testProjectFile, tempProjectContents);

                string sdkManifestFile = Path.Combine(sdkDirectory, "SdkManifest.xml");

                File.WriteAllText(sdkManifestFile, sdkManifestContents1);
                ITaskItem[] resolvedSDKReferences1 = RunBuildAndReturnResolvedSDKReferences(logger, testProjectFile, testDirectoryRoot);
                Assert.Equal(1, resolvedSDKReferences1.Length);

                Assert.True(resolvedSDKReferences1[0].GetMetadata("MoreInfo").Equals("http://msdn.microsoft.com/MySDK", StringComparison.OrdinalIgnoreCase));
                Assert.True(resolvedSDKReferences1[0].GetMetadata("MaxPlatformVersion").Equals("9.0", StringComparison.OrdinalIgnoreCase));
                Assert.True(resolvedSDKReferences1[0].GetMetadata("MinOSVersion").Equals("6.2.0", StringComparison.OrdinalIgnoreCase));
                Assert.True(resolvedSDKReferences1[0].GetMetadata("MaxOSVersionTested").Equals("6.2.3", StringComparison.OrdinalIgnoreCase));

                File.WriteAllText(sdkManifestFile, sdkManifestContents2);
                ITaskItem[] resolvedSDKReferences2 = RunBuildAndReturnResolvedSDKReferences(logger, testProjectFile, testDirectoryRoot);
                Assert.Equal(1, resolvedSDKReferences2.Length);

                Assert.True(resolvedSDKReferences2[0].GetMetadata("MoreInfo").Equals("http://msdn.microsoft.com/MySDK", StringComparison.OrdinalIgnoreCase));
                Assert.True(resolvedSDKReferences2[0].GetMetadata("MaxPlatformVersion").Equals(String.Empty));
                Assert.True(resolvedSDKReferences2[0].GetMetadata("MinOSVersion").Equals(String.Empty));
                Assert.True(resolvedSDKReferences2[0].GetMetadata("MaxOSVersionTested").Equals(String.Empty));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        private ITaskItem[] RunBuildAndReturnResolvedSDKReferences(ILogger logger, string testProjectFile, string testDirectoryRoot)
        {
            ProjectCollection pc = new ProjectCollection();
            ProjectInstance project = pc.LoadProject(testProjectFile).CreateProjectInstance();
            project.SetProperty("SDKReferenceDirectoryRoot", testDirectoryRoot);
            project.SetProperty("SDKReferenceRegistryRoot", "");

            IDictionary<string, TargetResult> targetResults = new Dictionary<string, TargetResult>();
            bool success = project.Build(new string[] { "GetResolvedSDKReferences" }, new ILogger[] { logger }, out targetResults);
            Assert.True(success);

            Assert.True(targetResults.ContainsKey("GetResolvedSDKReferences"));
            TargetResult result = targetResults["GetResolvedSDKReferences"];
            return result.Items;
        }
    }
}