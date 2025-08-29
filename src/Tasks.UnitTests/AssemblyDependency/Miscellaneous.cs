// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public sealed class Miscellaneous : ResolveAssemblyReferenceTestFixture
    {
        private static List<string> s_assemblyFolderExTestVersions = new List<string>
        {
            "v1.0",
            "v2.0.50727",
            "v3.0",
            "v3.5",
            "v4.0",
            "v4.0.2116",
            "v4.1",
            "v4.0.255",
            "v4.0.255.87",
            "v4.0.9999",
            "v4.0.0000",
            "v4.0001.0",
            "v4.0.2116.87",
            "v3.0SP1",
            "v3.0 BAZ",
            "v5.0",
            "v1",
            "v5",
            "v3.5.0.x86chk",
            "v3.5.1.x86chk",
            "v3.5.256.x86chk",
            "v",
            "1",
            "1.0",
            "1.0.0",
            "V3.5.0.0.0",
            "V3..",
            "V-1",
            "V9999999999999999",
            "Dan_rocks_bigtime",
            "v00001.0"
        };

        private string _fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which only contain the Microsoft.Build.Engine assembly in the allow list
        /// </summary>
        private string _engineOnlySubset =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
            "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which only contain the System.Xml assembly in the allow list
        /// </summary>
        private string _xmlOnlySubset =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "</FileList >";

        /// <summary>
        /// The contents of a subsetFile which contain both the Microsoft.Build.Engine and System.Xml assemblies in the allow list
        /// </summary>
        private string _engineAndXmlSubset =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
            "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
            "</FileList >";

        public Miscellaneous(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void VerifyPrimaryReferenceToBadImageDoesNotThrow()
        {
            ITaskItem x = new TaskItem(Path.Combine(s_myComponentsRootPath, "X.dll"));
            ITaskItem xpdb = new TaskItem(Path.Combine(s_myComponentsRootPath, "X.pdb"));
            ResolveAssemblyReference t = new()
            {
                BuildEngine = new MockEngine(),
                AllowedRelatedFileExtensions = new string[] { ".pdb" },
                Assemblies = new ITaskItem[] { xpdb },
                AssemblyFiles = new ITaskItem[] { x },
                SearchPaths = new string[] { "{RawFileName}" },
            };

            bool success = Execute(t);
            success.ShouldBeTrue();
        }

        /// <summary>
        /// Let us have the following dependency structure
        ///
        /// X which is in the gac, depends on Z which is not in the GAC
        ///
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to false
        ///
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to false and the parent of Z is in the GAC
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacFalseAllParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;

            if (NativeMethodsShared.IsWindows)
            {
                t.SearchPaths = new string[] { "{gac}", s_myComponentsRootPath };
            }
            else
            {
                t.SearchPaths = new string[] { s_myComponentsRootPath };
            }

            t.CopyLocalDependenciesWhenParentReferenceInGac = false;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
        }

        [Fact]
        public void ValidateFrameworkNameError()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { s_myComponentsRootPath };
            t.TargetFrameworkMoniker = "I am a random frameworkName";
            bool succeeded = Execute(t);

            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.InvalidParameter", "TargetFrameworkMoniker", t.TargetFrameworkMoniker, String.Empty);
            engine.AssertLogContains(message);
        }

        /// <summary>
        /// Let us have the following dependency structure
        ///
        /// X which is in the gac, depends on Z which is not in the GAC
        /// Y which is not in the gac, depends on Z which is not in the GAC
        ///
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to false
        ///
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to false but one of the parents of Z is not in the GAC and Z is not in the gac we should be copy local
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacFalseSomeParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                        new TaskItem("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;

            if (NativeMethodsShared.IsWindows)
            {
                t.SearchPaths = new string[] { "{gac}", s_myComponentsRootPath };
            }
            else
            {
                t.SearchPaths = new string[] { s_myComponentsRootPath };
            }

            t.CopyLocalDependenciesWhenParentReferenceInGac = false;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
            t.ResolvedFiles[1].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Make sure that when we parse the runtime version that if there is a bad one we default to 2.0.
        /// </summary>
        [Fact]
        public void TestSetRuntimeVersion()
        {
            Version parsedVersion = ResolveAssemblyReference.SetTargetedRuntimeVersion("4.0.21006");
            Assert.Equal(new Version("4.0.21006"), parsedVersion);

            parsedVersion = ResolveAssemblyReference.SetTargetedRuntimeVersion("BadVersion");
            Assert.Equal(new Version("2.0.50727"), parsedVersion);
        }

        /// <summary>
        /// Let us have the following dependency structure
        ///
        /// X which is in the gac, depends on Z which is not in the GAC
        ///
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to true
        ///
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to true and Z is not in the GAC it will be copy local true
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacTrueAllParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;

            if (NativeMethodsShared.IsWindows)
            {
                t.SearchPaths = new string[] { "{gac}", s_myComponentsRootPath };
            }
            else
            {
                t.SearchPaths = new string[] { s_myComponentsRootPath };
            }

            t.CopyLocalDependenciesWhenParentReferenceInGac = true;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Let us have the following dependency structure
        ///
        /// X which is in the gac, depends on Z which is not in the GAC
        /// Y which is not in the gac, depends on Z which is not in the GAC
        ///
        /// Let copyLocalDependenciesWhenParentReferenceInGac be set to true
        ///
        /// Since copyLocalDependenciesWhenParentReferenceInGac is set to true and Z is not in the GAC it will be copy local true
        /// </summary>
        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceInGacTrueSomeParentsInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        new TaskItem("X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                        new TaskItem("Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null")
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;

            if (NativeMethodsShared.IsWindows)
            {
                t.SearchPaths = new string[] { "{gac}", s_myComponentsRootPath };
            }
            else
            {
                t.SearchPaths = new string[] { s_myComponentsRootPath };
            }

            t.CopyLocalDependenciesWhenParentReferenceInGac = true;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
            t.ResolvedFiles[1].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
        }

        [Fact]
        public void CopyLocalDependenciesWhenParentReferenceNotInGac()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        // V not in GAC, depends on W (in GAC)
                        // V - CopyLocal should be true (resolved locally)
                        // W - CopyLocal should be false (resolved {gac})
                        new TaskItem("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = new string[] { "{gac}", @"c:\MyComponents" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.CopyLocalFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Test the legacy behavior for copy local (set to false when an assembly exists in the gac no matter
        /// where it was actually resolved). Sets DoNotCopyLocalIfInGac = true
        /// </summary>
        [Fact]
        public void CopyLocalLegacyBehavior()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
                    {
                        // V not in GAC, depends on W (in GAC)
                        // V - CopyLocal should be true (resolved locally)
                        // W - CopyLocal should be false (resolved from "c:\MyComponents" BUT exists in GAC, so false)
                        // (changed the order of the search paths to emulate this)
                        new TaskItem("V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"),
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.DoNotCopyLocalIfInGac = true;
            t.SearchPaths = new string[] { @"c:\MyComponents", "{gac}", };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.CopyLocalFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedFiles[0].GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Very basic test.
        /// </summary>
        [Fact]
        public void Basic()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.Basic() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Construct a list of assembly files.
            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(s_myMissingAssemblyAbsPath)
            };

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("MyPrivateAssembly"),
                new TaskItem("MyGacAssembly"),
                new TaskItem("MyCopyLocalAssembly"),
                new TaskItem("MyDontCopyLocalAssembly"),
                new TaskItem("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            assemblyNames[0].SetMetadata("RandomAttributeThatShouldBeForwarded", "1776");
            // Metadata which should NOT be forwarded
            assemblyNames[0].SetMetadata(ItemMetadataNames.imageRuntime, "FOO");
            assemblyNames[0].SetMetadata(ItemMetadataNames.winMDFile, "NOPE");
            assemblyNames[0].SetMetadata(ItemMetadataNames.winmdImplmentationFile, "IMPL");

            assemblyNames[1].SetMetadata("Private", "true");
            assemblyNames[2].SetMetadata("Private", "false");
            assemblyNames[4].SetMetadata("Private", "false");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            // Now, loop over the closure of dependencies and make sure we have what we need.
            bool enSatellitePdbFound = false;
            bool systemXmlFound = false;
            bool systemDataFound = false;
            bool systemFound = false;
            bool mscorlibFound = false;
            bool myGacAssemblyFound = false;
            bool myPrivateAssemblyFound = false;
            bool myCopyLocalAssemblyFound = false;
            bool myDontCopyLocalAssemblyFound = false;
            bool engbSatellitePdbFound = false;
            bool missingAssemblyFound = false;

            // Process the primary items.
            foreach (ITaskItem item in t.ResolvedFiles)
            {
                if (String.Equals(item.ItemSpec, Path.Combine(s_myVersion20Path, "System.XML.dll"), StringComparison.OrdinalIgnoreCase))
                {
                    systemXmlFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("1776", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                    item.GetMetadata("FusionName").ShouldBe("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
                    item.GetMetadata(ItemMetadataNames.imageRuntime).ShouldBe("v2.0.50727", StringCompareShould.IgnoreCase);
                    item.GetMetadata(ItemMetadataNames.winMDFile).ShouldBe("NOPE", StringCompareShould.IgnoreCase);
                    item.GetMetadata(ItemMetadataNames.winmdImplmentationFile).ShouldBe("IMPL", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(Path.Combine("v2.0.MyVersion", "System.Data.dll")))
                {
                    systemDataFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                    item.GetMetadata("FusionName").ShouldBe("System.Data, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(Path.Combine("v2.0.MyVersion", "MyGacAssembly.dll")))
                {
                    myGacAssemblyFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(s_myPrivateAssemblyRelPath))
                {
                    myPrivateAssemblyFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(Path.Combine("MyProject", "MyCopyLocalAssembly.dll")))
                {
                    myCopyLocalAssemblyFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(Path.Combine("MyProject", "MyDontCopyLocalAssembly.dll")))
                {
                    myDontCopyLocalAssemblyFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(s_myMissingAssemblyRelPath))
                {
                    missingAssemblyFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);

                    // Its debatable whether this file should be CopyLocal or not.
                    // It doesn't exist on disk, but is it ResolveAssemblyReference's job to make sure that it does?
                    // For now, let the default CopyLocal rules apply.
                    item.GetMetadata("CopyLocal").ShouldBe("true", StringCompareShould.IgnoreCase);
                    item.GetMetadata("FusionName").ShouldBe("MyMissingAssembly", StringCompareShould.IgnoreCase);
                }
                else if (String.Equals(item.ItemSpec, Path.Combine(s_myProjectPath, "System.Xml.dll"), StringComparison.OrdinalIgnoreCase))
                {
                    // The version of System.Xml.dll in C:\MyProject is an older version.
                    // This version is not a match. When want the current version which should have been in a different directory.
                    Assert.True(false, "Wrong version of System.Xml.dll matched--version was wrong");
                }
                else if (String.Equals(item.ItemSpec, Path.Combine(s_myProjectPath, "System.Data.dll"), StringComparison.OrdinalIgnoreCase))
                {
                    // The version of System.Data.dll in C:\MyProject has an incorrect PKT
                    // This version is not a match.
                    Assert.True(false, "Wrong version of System.Data.dll matched--public key token was wrong");
                }
                else
                {
                    Assert.True(false, String.Format("A new resolved file called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            // Process the dependencies.
            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (item.ItemSpec.EndsWith(Path.Combine("v2.0.MyVersion", "SysTem.dll")))
                {
                    systemFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                    item.GetMetadata("FusionName").ShouldBe("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
                }
                else if (item.ItemSpec.EndsWith(Path.Combine("v2.0.MyVersion", "mscorlib.dll")))
                {
                    mscorlibFound = true;
                    item.GetMetadata("DestinationSubDirectory").ShouldBe("", StringCompareShould.IgnoreCase);
                    item.GetMetadata("RandomAttributeThatShouldBeForwarded").ShouldBe("1776", StringCompareShould.IgnoreCase);
                    item.GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
                    item.GetMetadata(ItemMetadataNames.imageRuntime).ShouldBe("v2.0.50727", StringCompareShould.IgnoreCase);
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winMDFile));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));

                    // Notice how the following doesn't have 'version'. This is because all versions of mscorlib 'unify'
                    Assert.Equal(AssemblyRef.Mscorlib, item.GetMetadata("FusionName"));
                }
                else
                {
                    Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            // Process the related files.
            foreach (ITaskItem item in t.RelatedFiles)
            {
                Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
            }

            // Process the satellites.
            foreach (ITaskItem item in t.SatelliteFiles)
            {
                if (String.Equals(item.ItemSpec, Path.Combine(s_myVersion20Path, "en", "System.XML.resources.pdb"), StringComparison.OrdinalIgnoreCase))
                {
                    enSatellitePdbFound = true;
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.imageRuntime));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winMDFile));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                }
                else if (String.Equals(item.ItemSpec, Path.Combine(s_myVersion20Path, "en-GB", "System.XML.resources.pdb"), StringComparison.OrdinalIgnoreCase))
                {
                    engbSatellitePdbFound = true;
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.imageRuntime));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winMDFile));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                }
                else
                {
                    Assert.True(false, String.Format("A new dependency called '{0}' was found. If this is intentional, then add unittests above.", item.ItemSpec));
                }
            }

            Assert.False(enSatellitePdbFound); // "Expected to not find satellite pdb."
            Assert.True(systemXmlFound); // "Expected to find returned item."
            Assert.True(systemDataFound); // "Expected to find returned item."
            Assert.True(systemFound); // "Expected to find returned item."
            Assert.False(mscorlibFound); // "Expected to not find returned item."
            Assert.True(myGacAssemblyFound); // "Expected to find returned item."
            Assert.True(myPrivateAssemblyFound); // "Expected to find returned item."
            Assert.True(myCopyLocalAssemblyFound); // "Expected to find returned item."
            Assert.True(myDontCopyLocalAssemblyFound); // "Expected to find returned item."
            Assert.False(engbSatellitePdbFound); // "Expected to not find satellite pdb."
            Assert.True(missingAssemblyFound); // "Expected to find returned item."
        }

        /// <summary>
        /// Auxiliary enumeration for EmbedInteropTypes test.
        /// Defines indices for accessing test's data structures.
        /// </summary>
        private enum EmbedInteropTypes_Indices
        {
            MyMissingAssembly = 0,
            MyCopyLocalAssembly = 1,
            MyDontCopyLocalAssembly = 2,

            EndMarker
        };

        /// <summary>
        /// Make sure the imageruntime is correctly returned.
        /// </summary>
        [Fact]
        public void TestGetImageRuntimeVersion()
        {
            string imageRuntimeReportedByAsssembly = this.GetType().Assembly.ImageRuntimeVersion;
            string pathForAssembly = this.GetType().Assembly.Location;

            string inspectedRuntimeVersion = AssemblyInformation.GetRuntimeVersion(pathForAssembly);
            Assert.Equal(inspectedRuntimeVersion, imageRuntimeReportedByAsssembly);
        }

        /// <summary>
        /// Make sure the imageruntime is correctly returned.
        /// </summary>
        [Fact]
        public void TestGetImageRuntimeVersionBadPath()
        {
            string realFile = FileUtilities.GetTemporaryFile();
            try
            {
                string inspectedRuntimeVersion = AssemblyInformation.GetRuntimeVersion(realFile);
                Assert.Equal(inspectedRuntimeVersion, String.Empty);
            }
            finally
            {
                File.Delete(realFile);
            }
        }

        /// <summary>
        /// When specifying "EmbedInteropTypes" on a project targeting Fx higher than v4.0 -
        /// CopyLocal should be overridden to false
        /// </summary>
        [Fact]
        public void EmbedInteropTypes()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.Basic() test");

            // Construct a list of assembly files.
            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(s_myMissingAssemblyAbsPath)
            };

            assemblyFiles[0].SetMetadata("Private", "true");
            assemblyFiles[0].SetMetadata("EmbedInteropTypes", "true");

            // Construct a list of assembly names.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem("MyCopyLocalAssembly"),
                new TaskItem("MyDontCopyLocalAssembly")
            };

            assemblies[0].SetMetadata("Private", "true");
            assemblies[0].SetMetadata("EmbedInteropTypes", "true");
            assemblies[1].SetMetadata("Private", "false");
            assemblies[1].SetMetadata("EmbedInteropTypes", "true");

            // the matrix of TargetFrameworkVersion values we are testing
            string[] fxVersions =
            {
                "v2.0",
                "v3.0",
                "v3.5",
                "v4.0"
            };

            // expected ItemSpecs for corresponding assemblies
            string[] expectedItemSpec =
            {
                s_myMissingAssemblyRelPath,                 // MyMissingAssembly
                Path.Combine("MyProject", "MyCopyLocalAssembly.dll"),       // MyCopyLocalAssembly
                Path.Combine("MyProject", "MyDontCopyLocalAssembly.dll"),   // MyDontCopyLocalAssembly
            };

            // matrix of expected CopyLocal value per assembly per framework
            string[,] expectedCopyLocal =
            {
                // v2.0     v3.0     v3.5      v4.0
                { "true",  "true",  "true",  "false" },    // MyMissingAssembly
                { "true",  "true",  "true",  "false" },    // MyCopyLocalAssembly
                { "false", "false", "false", "false" }     // MyDontCopyLocalAssembly
            };

            int assembliesCount = (int)EmbedInteropTypes_Indices.EndMarker;

            // now let's verify our data structures are all set up correctly
            Assert.Equal(fxVersions.GetLength(0), expectedCopyLocal.GetLength(1)); // "fxVersions: test setup is incorrect"
            Assert.Equal(expectedItemSpec.Length, assembliesCount); // "expectedItemSpec: test setup is incorrect"
            Assert.Equal(expectedCopyLocal.GetLength(0), assembliesCount); // "expectedCopyLocal: test setup is incorrect"

            for (int i = 0; i < fxVersions.Length; i++)
            {
                // Create the engine.
                MockEngine engine = new MockEngine(_output);
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = engine;
                t.Assemblies = assemblies;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;

                string fxVersion = fxVersions[i];
                t.TargetFrameworkDirectories = new string[] { String.Format(@"c:\WINNT\Microsoft.NET\Framework\{0}.MyVersion", fxVersion) };
                t.TargetFrameworkVersion = fxVersion;
                Execute(t);

                bool[] assembliesFound = new bool[assembliesCount];

                // Now, process primary items and make sure we have what we need.
                foreach (ITaskItem item in t.ResolvedFiles)
                {
                    string copyLocal = item.GetMetadata("CopyLocal");

                    int j;
                    for (j = 0; j < assembliesCount; j++)
                    {
                        if (item.ItemSpec.EndsWith(expectedItemSpec[j]))
                        {
                            assembliesFound[j] = true;
                            string assemblyName = Enum.GetName(typeof(EmbedInteropTypes_Indices), j);
                            copyLocal.ShouldBe(expectedCopyLocal[j, i], fxVersion + ": unexpected CopyValue for " + assemblyName, StringCompareShould.IgnoreCase);
                            break;
                        }
                    }

                    if (j == assembliesCount)
                    {
                        Assert.True(false, String.Format("{0}: A new resolved file called '{1}' was found. If this is intentional, then add unittests above.", fxVersion, item.ItemSpec));
                    }
                }

                for (int j = 0; j < assembliesCount; j++)
                {
                    string assemblyName = Enum.GetName(typeof(EmbedInteropTypes_Indices), j);
                    Assert.True(assembliesFound[j], fxVersion + ": Expected to find returned item " + assemblyName);
                }
            }
        }

        /// <summary>
        /// If items lists are empty, then this is a NOP not a failure.
        /// </summary>
        [Fact]
        public void NOPForEmptyItemLists()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;

            bool succeeded = Execute(t);

            Assert.True(succeeded); // "Expected success."
        }

        /// <summary>
        /// If no related file extensions are input to RAR, .pdb and .xml should be used
        /// by default.
        /// </summary>
        [Fact]
        public void DefaultAllowedRelatedFileExtensionsAreUsed()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.DefaultRelatedFileExtensionsAreUsed() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Construct a list of assembly files.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem(s_assemblyFolder_SomeAssemblyDllPath)
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.dll"), t.ResolvedFiles[0].ItemSpec);

            // Process the related files.
            Assert.Equal(3, t.RelatedFiles.Length);

            bool pdbFound = false;
            bool xmlFound = false;
            bool priFound = false;

            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.pdb")))
                {
                    pdbFound = true;
                }
                if (item.ItemSpec.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.xml")))
                {
                    xmlFound = true;
                }
                if (item.ItemSpec.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.pri")))
                {
                    priFound = true;
                }
            }

            Assert.True(pdbFound && xmlFound && priFound); // "Expected to find .pdb, .xml, and .pri related files."
        }

        /// <summary>
        /// Externally resolved references do not get their related files identified by RAR. In the common
        /// nuget assets case, RAR cannot be the one to identify what to copy because RAR sees only the
        /// compile-time assets and not the runtime assets.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void RelatedFilesAreNotFoundForExternallyResolvedReferences(bool findDependenciesOfExternallyResolvedReferences)
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.DefaultRelatedFileExtensionsAreUsed() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Construct a list of assembly files.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem(s_assemblyFolder_SomeAssemblyDllPath)
            };

            assemblies[0].SetMetadata("ExternallyResolved", "true");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            t.FindDependenciesOfExternallyResolvedReferences = findDependenciesOfExternallyResolvedReferences; // does not impact related file search
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.dll"), t.ResolvedFiles[0].ItemSpec);
            Assert.Empty(t.RelatedFiles);
        }

        /// <summary>
        /// RAR should use any given related file extensions.
        /// </summary>
        [Fact]
        public void InputAllowedRelatedFileExtensionsAreUsed()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Miscellaneous.InputRelatedFileExtensionsAreUsed() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Construct a list of assembly files.
            ITaskItem[] assemblies = new TaskItem[]
            {
                new TaskItem(s_assemblyFolder_SomeAssemblyDllPath)
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblies;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            t.AllowedRelatedFileExtensions = new string[] { @".licenses", ".xml" }; // no .pdb or .config
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.dll"), t.ResolvedFiles[0].ItemSpec);

            // Process the related files.
            Assert.Equal(2, t.RelatedFiles.Length);

            bool licensesFound = false;
            bool xmlFound = false;
            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.licenses")))
                {
                    licensesFound = true;
                }
                if (item.ItemSpec.EndsWith(Path.Combine("AssemblyFolder", "SomeAssembly.xml")))
                {
                    xmlFound = true;
                }
            }

            Assert.True(licensesFound && xmlFound); // "Expected to find .licenses and .xml related files."
        }

        /// <summary>
        /// Simulate a CreateProject resolution. This is primarily for IO monitoring.
        /// </summary>
        private void SimulateCreateProjectAgainstWhidbeyInternal(string fxfolder)
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing SimulateCreateProjectAgainstWhidbey() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = new ITaskItem[] {
                new TaskItem("System"),
                new TaskItem("System.Deployment"),
                new TaskItem("System.Drawing"),
                new TaskItem("System.Windows.Forms"),
            };
            t.TargetFrameworkDirectories = new string[] { fxfolder };

            t.SearchPaths = new string[]
            {
                "{CandidateAssemblyFiles}",
                // Reference path
                "{HintPathFromItem}",
                @"{TargetFrameworkDirectory}",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{GAC}",
                "{RawFileName}"
            };

            bool succeeded = Execute(t);

            Assert.True(succeeded); // "Expected success."
        }

        /// <summary>
        /// Test with a standard path.
        /// </summary>
        [Fact]
        public void SimulateCreateProjectAgainstWhidbey()
        {
            SimulateCreateProjectAgainstWhidbeyInternal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45));
        }

        /// <summary>
        /// Test with a standard trailing-slash path.
        /// </summary>
        [Fact]
        public void SimulateCreateProjectAgainstWhidbeyWithTrailingSlash()
        {
            SimulateCreateProjectAgainstWhidbeyInternal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version45) + @"\");
        }

        /// <summary>
        /// Invalid candidate assembly files should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidCandidateAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.CandidateAssemblyFiles = new string[] { "|" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid assembly files should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.AssemblyFiles = new ITaskItem[] { new TaskItem("|") };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid assemblies param should not crash
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAssembliesParameter()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("|!@#$%::") };

            bool retval = Execute(t);

            // I think this should return true
            Assert.True(retval);

            // Should not crash.
        }

        /// <summary>
        /// Target framework path with a newline should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidTargetFrameworkDirectory()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing Regress286699_InvalidTargetFrameworkDirectory() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.TargetFrameworkDirectories = new string[] { "\nc:\\blah\\v2.0.1234" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid search path should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidSearchPath()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.SearchPaths = new string[] { "|" };

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Invalid app.config path should not crash.
        /// </summary>
        [Fact]
        public void Regress286699_InvalidAppConfig()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.AppConfigFile = "|";

            bool retval = Execute(t);

            Assert.False(retval);

            // Should not crash.
        }

        /// <summary>
        /// Make sure that nonexistent references are just eliminated.
        /// </summary>
        [Fact]
        public void NonExistentReference()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);
            t.Assemblies = new ITaskItem[] {
                new TaskItem("System.Xml"), new TaskItem("System.Nonexistent")
            };
            t.SearchPaths = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName), "{AssemblyFolders}", "{HintPathFromItem}", "{RawFileName}" };
            t.Execute();
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(0, String.Compare(ToolLocationHelper.GetPathToDotNetFrameworkFile("System.Xml.dll", TargetDotNetFrameworkVersion.Version45), t.ResolvedFiles[0].ItemSpec, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Consider this situation.
        ///
        ///    Assembly A
        ///     References: B (a simple name)
        ///
        ///    Assembly B
        ///     Assembly Name: B, PKT=aaa, Version=bbb, Culture=ccc
        ///
        /// A does _not_ want to load B because it simple name B does not match the
        /// B's assembly name.
        ///
        /// Because of this, we want to be sure that if A asks for B (as a simple name)
        /// that we don't find a strongly named assembly.
        /// </summary>
        [Fact]
        public void StrongWeakMismatchInDependency()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsOnSimpleA")
            };

            t.SearchPaths = new string[] { s_myAppRootPath, @"c:\MyStronglyNamed", @"c:\MyWeaklyNamed" };
            Execute(t);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(@"c:\MyWeaklyNamed\A.dll", t.ResolvedDependencyFiles[0].ItemSpec);
        }

        /// <summary>
        /// When a reference is marked as externally resolved, it is supposed to have provided
        /// everything needed as primary references and dependencies are therefore not searched
        /// as an optimization. This is a contrived case of a dangling dependency off of an
        /// externally resolved reference, so that we can observe that the dependency search is
        /// not performed in a test.
        /// </summary>
        [Fact]
        public void DependenciesOfExternallyResolvedReferencesAreNotSearched()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsOnSimpleA")
            };

            t.Assemblies[0].SetMetadata("ExternallyResolved", "true");

            t.SearchPaths = new string[] { s_myAppRootPath, @"c:\MyStronglyNamed", @"c:\MyWeaklyNamed" };
            Execute(t);
            Assert.Empty(t.ResolvedDependencyFiles);
        }

        /// <summary>
        /// If an Item has a HintPath and there is a {HintPathFromItem} in the SearchPaths
        /// property, then the task should be able to resolve an assembly there.
        /// </summary>
        [Fact]
        public void UseSuppliedHintPath()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing UseSuppliedHintPath() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            ITaskItem i = new TaskItem("My.Assembly");

            i.SetMetadata("HintPath", @"C:\myassemblies\My.Assembly.dll");
            i.SetMetadata("Baggage", @"Carry-On");
            t.Assemblies = new ITaskItem[] { i };
            t.SearchPaths = DefaultPaths;
            Execute(t);
            Assert.Equal(@"C:\myassemblies\My.Assembly.dll", t.ResolvedFiles[0].ItemSpec);
            Assert.Single(t.ResolvedFiles);

            // All attributes, including HintPath, should be forwarded from input to output
            Assert.Equal(@"C:\myassemblies\My.Assembly.dll", t.ResolvedFiles[0].GetMetadata("HintPath"));
            Assert.Equal(@"Carry-On", t.ResolvedFiles[0].GetMetadata("Baggage"));
        }

        /// <summary>
        /// Regress this devices bug.
        /// If a simple name is provided then we need to accept the first simple file name match.
        /// Devices frameworks files are signed with a different PK so there should be no unification
        /// with normal fx files.
        /// </summary>
        [Fact]
        public void Regress200872()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("mscorlib") };
            t.SearchPaths = new string[]
            {
                s_myVersionPocket20Path,
                s_myVersion20Path
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersionPocket20Path, "mscorlib.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Do the most basic AssemblyFoldersEx resolve.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExBasic()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyGrid") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponents\MyGrid.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Verify that higher alphabetical values for a component are chosen over lower alphabetic values of a component.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExVerifyComponentFolderSorting()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("CustomComponent") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponentsB\CustomComponent.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// If the target framework version provided by the targets file doesn't begin
        /// with the letter "v", we should tolerate it and treat it as if it does.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExTargetFrameworkVersionDoesNotBeginWithV()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyGrid") };
            t.SearchPaths = new string[] { @"{Registry:Software\Microsoft\.NetFramework,2.0,AssemblyFoldersEx}" };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponents\MyGrid.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Microsoft\.NetFramework,2.0,AssemblyFoldersEx}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target AMD64 and try to get an assembly out of the X86 directory.
        /// Expect it not to resolve and get a message on the console
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchDoesNotMatch()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "AMD64";
            Execute(t);

            Assert.Empty(t.ResolvedFiles);
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.TargetedProcessorArchitectureDoesNotMatch", @"C:\Regress714052\X86\A.dll", "X86", "AMD64");
            mockEngine.AssertLogContains(message);
        }

        /// <summary>
        /// Regress DevDiv Bugs 714052.
        ///
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target MSIL and get an assembly out of the X86 directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchMSILX86()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "None";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a warning.
        /// </summary>
        [WindowsOnlyFact]
        public void VerifyProcessArchitectureMismatchWarning()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Warning";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a warning.
        /// </summary>
        [WindowsOnlyFact]
        public void VerifyProcessArchitectureMismatchWarningDefault()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Verify if there is a mismatch between what the project targets and the architecture of the resolved primary reference log a error.
        /// </summary>
        [WindowsOnlyFact]
        public void VerifyProcessArchitectureMismatchError()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A"), new TaskItem("B") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "MSIL";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";
            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(2, mockEngine.Errors);
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"A", "X86");
            mockEngine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArch", "MSIL", @"B", "X86");
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target None and get an assembly out of the X86 directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchNoneX86()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "NONE";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// If we are targeting NONE and there are two assemblies with the same name then we want to pick the first one rather than look for an assembly which
        /// has a MSIL architecture or a NONE architecture. NONE means you do not care what architecture is picked.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchNoneMix()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MIX}" };
            t.TargetProcessorArchitecture = "NONE";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";  // should not do anything
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(0, mockEngine.Warnings);
            Assert.Equal(0, mockEngine.Errors);
            Assert.Equal(@"C:\Regress714052\Mix\a.winmd", t.ResolvedFiles[0].ItemSpec, true);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,Mix}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly.
        /// When targeting MSIL we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target MSIL and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchMSILLastFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\Regress714052\MSIL\A.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly.
        /// When targeting None we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target None and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchNoneLastFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\Regress714052\MSIL\A.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", StringCompareShould.IgnoreCase);
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Assume the folders are searched in the order  A and B.  A contains an x86 assembly and B contains an MSIL assembly.
        /// When targeting X86 we want to return the MSIL assembly even if we find one in a previous folder first.
        /// Target MSIL and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchX86FirstFolder()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEx}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\Regress714052\X86\A.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,AssemblyFoldersEX}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target X86 and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchX86MSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target X86 and get an assembly out of the None directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchX86None()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,None}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target None and get an assembly out of the None directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchNoneNone()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,None}", StringCompareShould.IgnoreCase);
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target MSIL and get an assembly out of the None directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArcMSILNone()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,None}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,None}", StringCompareShould.IgnoreCase);
        }
        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target None and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchNoneMSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "None";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target MSIL and get an assembly out of the MSIL directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchMSILMSIL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine mockEngine = new MockEngine(_output);
            t.BuildEngine = mockEngine;

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,MSIL}" };
            t.TargetProcessorArchitecture = "MSIL";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,MSIL}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// The above but now requires us to make sure the processor architecture of what we are targeting matches what we are resolving.
        ///
        /// Target X86 and get an assembly out of the X86 directory.
        ///
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExProcessorArchMatches()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("A") };
            t.SearchPaths = new string[] { @"{Registry:Software\Regress714052,v2.0.0,X86}" };
            t.TargetProcessorArchitecture = "X86";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\Regress714052\X86\A.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Regress714052,v2.0.0,X86}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// If the target framework version specified in the registry search path
        /// provided by the targets file has some bogus value, we should just ignore it.
        ///
        /// This means if there are remaining search paths to inspect, we should
        /// carry on and inspect those.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExTargetFrameworkVersionBogusValue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            ITaskItem assemblyToResolve = new TaskItem("MyGrid");
            assemblyToResolve.SetMetadata("HintPath", @"C:\MyComponents\MyGrid.dll");
            t.Assemblies = new ITaskItem[] { assemblyToResolve };
            t.SearchPaths = new string[] { @"{Registry:Software\Microsoft\.NetFramework,x.y.z,AssemblyFoldersEx}", "{HintPathFromItem}" };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal("{HintPathFromItem}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom")); // "Assembly should have been resolved from HintPathFromItem!"
        }

        /// <summary>
        /// Tolerate keys like v2.0.x86chk.
        /// </summary>
        [WindowsOnlyFact]
        public void Regress357227_AssemblyFoldersExAgainstRawDrop()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyRawDropControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyRawDropControls\MyRawDropControl.dll", t.ResolvedFiles[0].ItemSpec);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Matches that exist only in the HKLM hive.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExHKLM()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyHKLMControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponents\HKLM Components\MyHKLMControl.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Matches that exist in both HKLM and HKCU should favor HKCU
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExHKCUTrumpsHKLM()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing AssemblyFoldersExHKCUTrumpsHKLM() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyHKLMandHKCUControl") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// When matches that have v3.0 (future) and v2.0 (current) versions, the 2.0 version wins.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExFutureTargetNDPVersionsDontMatch()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithFutureTargetNDPVersion") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myComponentsV20Path, "MyControlWithFutureTargetNDPVersion.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If there is no v2.0 (current target NDP) match, then v1.0 should match.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExMatchBackVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyNDP1Control") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myComponentsV10Path, "MyNDP1Control.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If there is a 2.0 and a 1.0 then match 2.0.
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExCurrentTargetVersionTrumpsPastTargetVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithPastTargetNDPVersion") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myComponentsV20Path, "MyControlWithPastTargetNDPVersion.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If a control has a service pack then that wins over the control itself
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExServicePackTrumpsBaseVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyControlWithServicePack") };
            t.SearchPaths = DefaultPaths;

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\MyComponentServicePack2\MyControlWithServicePack.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test MaxOSVersion condition
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExConditionFilterMaxOS()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing AssemblyFoldersExConditionFilterMaxOS() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                s_myVersion20Path,
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,OSVersion=4.0.0:Platform=3C41C503-53EF-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\V1ControlSP1\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test MinOSVersion condition
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExConditionFilterMinOS()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                s_myVersion20Path,
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,OSVersion=5.1.0:Platform=3C41C503-53EF-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\V1Control\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

#if FEATURE_WIN32_REGISTRY
        [Fact]
        public void GatherVersions10DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v1.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(3, returnedVersions.Count);
            Assert.Equal("v1.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[2].RegistryKey);
        }

        [Fact]
        public void GatherVersions20DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v2.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(4, returnedVersions.Count);
            Assert.Equal("v2.0.50727", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[3].RegistryKey);
        }

        [Fact]
        public void GatherVersions30DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(7, returnedVersions.Count);

            Assert.Equal("v3.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v3.0SP1", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v3.0 BAZ", (string)returnedVersions[6].RegistryKey);
        }

        [Fact]
        public void GatherVersionsVDotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(27, returnedVersions.Count);

            Assert.Equal("v5.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v5", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0001.0", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.1", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[12].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[13].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[14].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[15].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[16].RegistryKey);
            Assert.Equal("v3.0SP1", (string)returnedVersions[17].RegistryKey);
            Assert.Equal("v3.0 BAZ", (string)returnedVersions[18].RegistryKey);
            Assert.Equal("v3.5.0.x86chk", (string)returnedVersions[19].RegistryKey);
            Assert.Equal("v3.5.1.x86chk", (string)returnedVersions[20].RegistryKey);
            Assert.Equal("v3.5.256.x86chk", (string)returnedVersions[21].RegistryKey);
            Assert.Equal("v", (string)returnedVersions[22].RegistryKey);
            Assert.Equal("V3.5.0.0.0", (string)returnedVersions[23].RegistryKey);
            Assert.Equal("V3..", (string)returnedVersions[24].RegistryKey);
            Assert.Equal("V-1", (string)returnedVersions[25].RegistryKey);
            Assert.Equal("v9999999999999999", (string)returnedVersions[26].RegistryKey, true);
        }

        [Fact]
        public void GatherVersions35DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(10, returnedVersions.Count);
            Assert.Equal("v3.5", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v3.5.0.x86chk", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v3.5.1.x86chk", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v3.5.256.x86chk", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("V3.5.0.0.0", (string)returnedVersions[9].RegistryKey);
        }

        [Fact]
        public void GatherVersions40DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(10, returnedVersions.Count);
            Assert.Equal("v4.0.9999", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[9].RegistryKey);
        }

        [Fact]
        public void GatherVersions400DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(11, returnedVersions.Count);
            Assert.Equal("v4.0.0000", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[10].RegistryKey);
        }

        [Fact]
        public void GatherVersions41DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(14, returnedVersions.Count);

            Assert.Equal("v4.1", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[12].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[13].RegistryKey);
        }

        [Fact]
        public void GatherVersions410DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.1.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(15, returnedVersions.Count);

            Assert.Equal("v4.0001.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v4.1", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[12].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[13].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[14].RegistryKey);
        }

        [Fact]
        public void GatherVersions40255DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v4.0.255", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(13, returnedVersions.Count);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[12].RegistryKey);
        }

        [Fact]
        public void GatherVersions5DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5.0", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(17, returnedVersions.Count);

            Assert.Equal("v5.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v5", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0001.0", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.1", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[12].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[13].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[14].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[15].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[16].RegistryKey);
        }

        [Fact]
        public void GatherVersionsv5DotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v5", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Equal(17, returnedVersions.Count);

            Assert.Equal("v5.0", (string)returnedVersions[0].RegistryKey);
            Assert.Equal("v5", (string)returnedVersions[1].RegistryKey);
            Assert.Equal("v4.0001.0", (string)returnedVersions[2].RegistryKey);
            Assert.Equal("v4.1", (string)returnedVersions[3].RegistryKey);
            Assert.Equal("v4.0.255.87", (string)returnedVersions[4].RegistryKey);
            Assert.Equal("v4.0.255", (string)returnedVersions[5].RegistryKey);
            Assert.Equal("v4.0.0000", (string)returnedVersions[6].RegistryKey);
            Assert.Equal("v4.0.9999", (string)returnedVersions[7].RegistryKey);
            Assert.Equal("v4.0.2116.87", (string)returnedVersions[8].RegistryKey);
            Assert.Equal("v4.0.2116", (string)returnedVersions[9].RegistryKey);
            Assert.Equal("v4.0", (string)returnedVersions[10].RegistryKey);
            Assert.Equal("v3.5", (string)returnedVersions[11].RegistryKey);
            Assert.Equal("v3.0", (string)returnedVersions[12].RegistryKey);
            Assert.Equal("v2.0.50727", (string)returnedVersions[13].RegistryKey);
            Assert.Equal("v1.0", (string)returnedVersions[14].RegistryKey);
            Assert.Equal("v1", (string)returnedVersions[15].RegistryKey);
            Assert.Equal("v00001.0", (string)returnedVersions[16].RegistryKey);
        }

        [Fact]
        public void GatherVersions35x86chkDotNet()
        {
            List<ExtensionFoldersRegistryKey> returnedVersions = AssemblyFoldersEx.GatherVersionStrings("v3.5.0.x86chk", s_assemblyFolderExTestVersions);

            Assert.NotNull(returnedVersions);
            Assert.Single(returnedVersions);

            Assert.Equal("v3.5.0.x86chk", (string)returnedVersions[0].RegistryKey);
        }
#endif

        /// <summary>
        /// Conditions (OSVersion/Platform) can be passed in SearchPaths to filter the result.
        /// Test Platform condition
        /// </summary>
        [WindowsOnlyFact]
        public void AssemblyFoldersExConditionFilterPlatform()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("MyDeviceControlAssembly") };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                s_myVersion20Path,
                @"{Registry:Software\Microsoft\.NETCompactFramework,v2.0,PocketPC\AssemblyFoldersEx,Platform=3C41C503-X-4c2a-8DD4-A8217CAD115E}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            SetupAssemblyFoldersExTestConditionRegistryKey();

            try
            {
                Execute(t);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(@"C:\V1Control\MyDeviceControlAssembly.dll", t.ResolvedFiles[0].ItemSpec);
        }

        private void SetupAssemblyFoldersExTestConditionRegistryKey()
        {
            // Setup the following registry keys:
            //  HKCU\SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl
            //          @c:\V1Control
            //          @MinOSVersion=5.0.0
            //  HKCU\SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234
            //          @c:\V1ControlSP1
            //          @MinOSVersion=4.0.0
            //          @MaxOSVersion=4.1.0
            //          @Platform=4118C335-430C-497f-BE48-11C3316B135E;3C41C503-53EF-4c2a-8DD4-A8217CAD115E

            RegistryKey baseKey = Registry.CurrentUser;
            RegistryKey folderKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl");
            folderKey.SetValue("", @"C:\V1Control");
            folderKey.SetValue("MinOSVersion", "5.0.0");

            RegistryKey servicePackKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234");
            servicePackKey.SetValue("", @"C:\V1ControlSP1");
            servicePackKey.SetValue("MinOSVersion", "4.0.0");

            servicePackKey.SetValue("MaxOSVersion", "4.1.0");
            servicePackKey.SetValue("Platform", "4118C335-430C-497f-BE48-11C3316B135E;3C41C503-53EF-4c2a-8DD4-A8217CAD115E");
        }

        private void RemoveAssemblyFoldersExTestConditionRegistryKey()
        {
            RegistryKey baseKey = Registry.CurrentUser;
            try
            {
                baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// CandidateAssemblyFiles are extra files passed in through the CandidateAssemblyFiles
        /// that should be considered for matching when search paths contains {CandidateAssemblyFiles}
        /// </summary>
        [Fact]
        public void CandidateAssemblyFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("System.XML") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { Path.Combine(s_myVersion20Path, "System.Xml.dll") };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure three part version numbers put on the required target framework do not cause a problem.
        /// </summary>
        [Fact]
        public void ThreePartVersionNumberRequiredFrameworkHigherThanTargetFramework()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);
            TaskItem item = new TaskItem("System.XML");
            item.SetMetadata("RequiredTargetFramework", "v4.0.255");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { Path.Combine(s_myVersion20Path, "System.Xml.dll") };
            t.TargetFrameworkVersion = "v4.0";
            Execute(t);

            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Make sure three part version numbers put on the required target framework do not cause a problem.
        /// </summary>
        [Fact]
        public void ThreePartVersionNumberRequiredFrameworkLowerThanTargetFramework()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);
            TaskItem item = new TaskItem("System.XML");
            item.SetMetadata("RequiredTargetFramework", "v4.0.255");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { Path.Combine(s_myVersion20Path, "System.Xml.dll") };
            t.TargetFrameworkVersion = "v4.0.256";
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Try a candidate assembly file that has an extension but no base name.
        /// </summary>
        [Fact]
        public void Regress242970()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem("System.XML") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[]
            {
                @"NonUI\testDirectoryRoot\.hiddenfile",
                @"NonUI\testDirectoryRoot\.dll",
                Path.Combine(s_myVersion20Path, "System.Xml.dll")
            };

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);

            // For {CandidateAssemblyFiles} we don't even want to see a comment logged for files with non-standard extensions.
            // This is because {CandidateAssemblyFiles} is very likely to contain non-assemblies and its best not to clutter
            // up the log.
            engine.AssertLogDoesntContain(
                String.Format(".hiddenfile"));

            // ...but we do want to see a log entry for standard extensions, even if the base file name is empty.
            engine.AssertLogContains(
                String.Format(@"NonUI\testDirectoryRoot\.dll"));
        }

        /// <summary>
        /// If a file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name.
        /// </summary>
        [Fact]
        public void RawFileName()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing RawFileName() test");

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem(Path.Combine(s_myVersion20Path, "System.Xml.dll")) };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                "{TargetFrameworkDirectory}",
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}",
                "{GAC}"
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure when there are duplicate entries in the redist list, with different versions of ingac (true and false) that we will not read in two entries,
        /// we will instead pick the one with ingac true and ignore the ingac false entry.   If there is one of more entries in the redist list with ingac false
        /// and no entries with ingac true for a given assembly then we should only have one entry with ingac false.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingForRedistLists()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.XML' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='Microsoft.BuildEngine' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
              "</FileList >";

            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContentsDuplicates);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, null);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Equal(4, assembliesReadIn.Count);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Make sure that if there are different SimpleName then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandling()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.XML' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 1, 0);
        }

        /// <summary>
        /// Make sure that if there are different IsRedistRoot then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentIsRedistRoot()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' IsRedistRoot='true'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.XML' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' IsRedistRoot='false' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different IsRedistRoot then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentName()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true'/>" +
                  "<File AssemblyName='MyAssembly' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='AnotherAssembly' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different culture then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentCulture()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-EN' FileVersion='2.0.50727.208' InGAC='true'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='fr-FR' FileVersion='2.0.50727.208' InGAC='true' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different public key tokens then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentPublicKeyToken()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3d' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 0);
        }

        /// <summary>
        /// Make sure that if there are different retargetable flags then they will not be considered duplicates.
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentRetargetable()
        {
            string fullRedistListContentsDuplicates =

              "<FileList Redist='Microsoft-Windows-CLRCoreComp'>" +
                  "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                  "</Remap>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='Yes'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='Yes'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' Retargetable='No'/>" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
              "</FileList >";

            ExpectRedistEntries(fullRedistListContentsDuplicates, 2, 1);
        }
        /// <summary>
        /// Make sure that if there are different versions that they are all picked
        /// </summary>
        [Fact]
        public void TestDuplicateHandlingDifferentVersion()
        {
            string fullRedistListContentsDuplicates =
              "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                  "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                 "<Remap>" +
                     "<From AssemblyName='System.Xml2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                        "<To AssemblyName='Remapped2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>" +
                  "<File AssemblyName='System.Xml' Version='3.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' />" +
                  "<File AssemblyName='System.Xml' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208'/>" +
                  "<Remap>" +
                     "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                        "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a33' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>" +
            "</FileList >";

            List<AssemblyEntry> entries = ExpectRedistEntries(fullRedistListContentsDuplicates, 3, 2);
        }

        /// <summary>
        /// Expect to read in a certain number of redist list entries, this is factored out because we went to test a number of input combinations which will all result in entries returned.
        /// </summary>
        private static List<AssemblyEntry> ExpectRedistEntries(string fullRedistListContentsDuplicates, int numberOfExpectedEntries, int numberofExpectedRemapEntries)
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
            List<AssemblyRemapping> remapEntries = new List<AssemblyRemapping>();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContentsDuplicates);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remapEntries);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Equal(assembliesReadIn.Count, numberOfExpectedEntries);
                Assert.Equal(remapEntries.Count, numberofExpectedRemapEntries);
            }
            finally
            {
                File.Delete(redistFile);
            }

            return assembliesReadIn;
        }

        /// <summary>
        /// Test the basics of reading in the remapping section
        /// </summary>
        [Fact]
        public void TestRemappingSectionBasic()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Single(remap);

                AssemblyRemapping pair = remap[0];
                Assert.Equal("System.Xml", pair.From.Name);
                Assert.Equal("Remapped", pair.To.Name);
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are multiple "To" elements under the "From" element then pick the first one.
        /// </summary>
        [Fact]
        public void MultipleToElementsUnderFrom()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "<To AssemblyName='RemappedSecond' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Single(remap);

                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.Equal("System.Xml", pair.From.Name);
                Assert.Equal("Remapped", pair.To.Name);
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are two from tags which map to the same "To" element then we still need two entries.
        /// </summary>
        [Fact]
        public void DifferentFromsToSameTo()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                   "<From AssemblyName='System.Core' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                    "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Equal(2, remap.Count);

                foreach (AssemblyRemapping pair in remap)
                {
                    Assert.Equal("Remapped", pair.To.Name);
                    Assert.False(pair.To.Retargetable);
                }
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If there are two identical entries then pick the first one
        /// </summary>
        [Fact]
        public void DuplicateEntries()
        {
            string fullRedistListContents =
              "<Remap>" +
                  "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                     "</From>" +
                   "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped2' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
                    "</From>" +
                 "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Single(remap);

                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.Equal("Remapped", pair.To.Name);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the remapping section is empty
        /// </summary>
        [Fact]
        public void EmptyRemapping()
        {
            string fullRedistListContents = "<Remap/>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Empty(remap);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the we have a "from" element but no "to" element. We expect that to be ignored
        /// </summary>
        [Fact]
        public void FromElementButNoToElement()
        {
            string fullRedistListContents =
        "<Remap>" +
         "<From AssemblyName='System.Core' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'/>" +
         "<From AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'>" +
                     "<To AssemblyName='Remapped' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='en-us' FileVersion='2.0.50727.208' InGAC='false'/>" +
         "</From>" +
        "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Single(remap);

                AssemblyRemapping pair = remap.First<AssemblyRemapping>();
                Assert.Equal("System.Xml", pair.From.Name);
                Assert.Equal("Remapped", pair.To.Name);
                Assert.True(pair.From.Retargetable);
                Assert.False(pair.To.Retargetable);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test if the we have a "To" element but no "from" element. We expect that to be ignored
        /// </summary>
        [Fact]
        public void ToElementButNoFrom()
        {
            string fullRedistListContents =
        "<Remap>" +
         "<To AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' Retargetable='Yes'/>" +
        "</Remap>";

            string redistFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(redistFile, fullRedistListContents);

                AssemblyTableInfo info = new AssemblyTableInfo(redistFile, String.Empty);
                List<AssemblyEntry> assembliesReadIn = new List<AssemblyEntry>();
                List<AssemblyRemapping> remap = new List<AssemblyRemapping>();
                List<Exception> errors = new List<Exception>();
                List<string> errorFileNames = new List<string>();
                RedistList.ReadFile(info, assembliesReadIn, errors, errorFileNames, remap);
                Assert.Empty(errors); // "Expected no Errors"
                Assert.Empty(errorFileNames); // "Expected no Error file names"
                Assert.Empty(remap);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If a relative file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name and make it a full path.
        /// </summary>
        [Fact]
        public void RawFileNameRelative()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"..\RawFileNameRelative\System.Xml.dll") };
                t.SearchPaths = new string[] { "{RawFileName}" };
                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }

        /// <summary>
        /// If a relative searchPath is passed in through the search path parameter
        /// then try to resolve the file but make sure it is a full name
        /// </summary>
        [Fact]
        public void RelativeDirectoryResolver()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                t.Assemblies = new ITaskItem[] { new TaskItem(@"System.Xml.dll") };
                t.SearchPaths = new string[] { "..\\RawFileNameRelative" };
                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }

        /// <summary>
        /// If a relative file name is passed in through the HintPath then try to resolve directly to that file name and make it a full path.
        /// </summary>
        [Fact]
        public void HintPathRelative()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            string testPath = Path.Combine(Path.GetTempPath(), @"RawFileNameRelative");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                TaskItem taskItem = new TaskItem(AssemblyRef.SystemXml);
                taskItem.SetMetadata("HintPath", @"..\RawFileNameRelative\System.Xml.dll");

                t.Assemblies = new ITaskItem[] { taskItem };
                t.SearchPaths = new string[] { "{HintPathFromItem}" };
                Execute(t);

                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }
        /// <summary>
        /// Make sure we do not crash if a raw file name is passed in and the specific version metadata is set
        /// </summary>
        [Fact]
        public void RawFileNameWithSpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            ITaskItem taskItem = new TaskItem(Path.Combine(s_myVersion20Path, "System.Xml.dll"));
            taskItem.SetMetadata("SpecificVersion", "false");

            t.Assemblies = new ITaskItem[] { taskItem };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Make sure we do not crash if a raw file name is passed in and the specific version metadata is set
        /// </summary>
        [Fact]
        public void RawFileNameWithSpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            ITaskItem taskItem = new TaskItem(Path.Combine(s_myVersion20Path, "System.Xml.dll"));
            taskItem.SetMetadata("SpecificVersion", "true");

            t.Assemblies = new ITaskItem[] { taskItem };
            t.SearchPaths = new string[]
            {
                "{RawFileName}",
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If the user passed in a file name but no {RawFileName} was specified.
        /// </summary>
        [Fact]
        public void Regress363340_RawFileNameMissing()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(Path.Combine(s_myVersion20Path, "System.Xml.dll")),
                new TaskItem(@"System.Data")
            };

            t.SearchPaths = new string[]
            {
                s_myVersion20Path,
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Data.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If the reference include looks like a file name rather than a properly formatted reference and a good hint path is provided,
        /// good means the hintpath points to a file which exists on disk. Then we were getting an exception
        /// because assemblyName was null and we were comparing the assemblyName from the hintPath to the null assemblyName.
        /// </summary>
        [Fact]
        public void Regress444793()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            TaskItem item = new TaskItem(@"c:\DoesntExist\System.Xml.dll");
            item.SetMetadata("HintPath", Path.Combine(s_myVersion20Path, "System.Data.dll"));
            item.SetMetadata("SpecificVersion", "true");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            bool succeeded = Execute(t);
            Assert.True(succeeded);
            engine.AssertLogDoesntContain("MSB4018");

            engine.AssertLogContains(
                String.Format(AssemblyResources.GetString("General.MalformedAssemblyName"), "c:\\DoesntExist\\System.Xml.dll"));
        }

        /// <summary>
        /// If a file name is passed in through the Assemblies parameter and the search paths contains {RawFileName}
        /// then try to resolve directly to that file name.
        /// </summary>
        [Fact]
        public void RawFileNameDoesntExist()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem(@"c:\DoesntExist\System.Xml.dll") };
            t.SearchPaths = new string[] { "{RawFileName}" };

            bool succeeded = Execute(t);
            Assert.True(succeeded);
            engine.AssertLogContains(
                String.Format(AssemblyResources.GetString("General.MalformedAssemblyName"), "c:\\DoesntExist\\System.Xml.dll"));
        }

        /// <summary>
        /// If a candidate file has a different base name, then this should not be a match.
        /// </summary>
        [Fact]
        public void CandidateAssemblyFilesDifferentBaseName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem("VendorAssembly") };
            t.SearchPaths = new string[] { "{CandidateAssemblyFiles}" };
            t.CandidateAssemblyFiles = new string[] { @"Dlls\ProjectItemAssembly.dll" };

            Execute(t);

            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Given a strong name, resolve it to a location in the GAC if possible.
        /// </summary>
        [Fact]
        public void ResolveToGAC()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] { new TaskItem("System") };
            t.TargetedRuntimeVersion = typeof(Object).Assembly.ImageRuntimeVersion;
            t.SearchPaths = new string[] { "{GAC}" };
            bool succeeded = t.Execute();
            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Given a strong name, resolve it to a location in the GAC if possible.
        /// </summary>
        [Fact]
        public void ResolveToGACSpecificVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            TaskItem item = new TaskItem("System");
            item.SetMetadata("SpecificVersion", "true");
            t.Assemblies = new ITaskItem[] { item };
            t.SearchPaths = new string[] { "{GAC}" };
            t.TargetedRuntimeVersion = new Version("0.5.0.0").ToString();
            bool succeeded = t.Execute();
            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Verify that when we are calculating the search paths for a dependency that we take into account where the parent assembly was resolved from
        /// for example if the parent assembly was resolved from the GAC or AssemblyFolders then we do not want to look in the parent assembly directory
        /// instead we want to let the assembly be resolved normally so that the GAC and AF checks will work.
        /// </summary>
        [Fact]
        public void ParentAssemblyResolvedFromAForGac()
        {
            var parentReferenceFolders = new List<string>();
            var referenceList = new List<Reference>();

            var taskItem = new TaskItem("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            var reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference.FullPath = "c:\\AssemblyFolders\\Microsoft.VisualStudio.Interopt.dll";
            reference.ResolvedSearchPath = "{AssemblyFolders}";

            Reference reference2 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference2.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference2.FullPath = "c:\\SomeOtherFolder\\Microsoft.VisualStudio.Interopt2.dll";
            reference2.ResolvedSearchPath = "c:\\SomeOtherFolder";

            Reference reference3 = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference3.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            reference3.FullPath = "c:\\SomeOtherFolder\\Microsoft.VisualStudio.Interopt3.dll";
            reference3.ResolvedSearchPath = "{GAC}";

            referenceList.Add(reference);
            referenceList.Add(reference2);
            referenceList.Add(reference3);

            foreach (Reference parentReference in referenceList)
            {
                ReferenceTable.CalculateParentAssemblyDirectories(parentReferenceFolders, parentReference);
            }

            Assert.Single(parentReferenceFolders);
            Assert.Equal(reference2.ResolvedSearchPath, parentReferenceFolders[0]);
        }

        /// <summary>
        /// Generate a fake reference which has been resolved from the gac. We will use it to verify the creation of the exclusion list.
        /// </summary>
        /// <returns></returns>
        private ReferenceTable GenerateTableWithAssemblyFromTheGlobalLocation(string location)
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, Array.Empty<string>(), null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null, null,
#if FEATURE_WIN32_REGISTRY
                null, null, null,
#endif
                null, null, new Version("4.0"), null, null, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false, null);

            AssemblyNameExtension assemblyNameExtension = new AssemblyNameExtension(new AssemblyName("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
            TaskItem taskItem = new TaskItem("Microsoft.VisualStudio.Interopt, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            // "Resolve the assembly from the gac"
            reference.FullPath = "c:\\Microsoft.VisualStudio.Interopt.dll";
            reference.ResolvedSearchPath = location;
            referenceTable.AddReference(assemblyNameExtension, reference);

            assemblyNameExtension = new AssemblyNameExtension(new AssemblyName("Team.System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"));
            taskItem = new TaskItem("Team, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

            reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");

            // "Resolve the assembly from the gac"
            reference.FullPath = "c:\\Team.System.dll";
            reference.ResolvedSearchPath = location;
            referenceTable.AddReference(assemblyNameExtension, reference);
            return referenceTable;
        }

        /// <summary>
        /// Given a reference that resolves to a bad image, we should get a warning and
        /// no reference. We don't want an exception.
        /// </summary>
        [Fact]
        public void ResolveBadImageInPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("BadImage")
            };
            t.Assemblies[0].SetMetadata("Private", "true");
            t.SearchPaths = new string[] { s_myVersion20Path };
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            Execute(t);

            // There should be no resolved file, because the image was bad.
            Assert.Empty(t.ResolvedFiles);

            // There should be no related files either.
            Assert.Empty(t.RelatedFiles);
            engine.AssertLogDoesntContain("BadImage.pdb");
            engine.AssertLogDoesntContain("HRESULT");

            // There should have been one warning about the exception.
            Assert.Equal(1, engine.Warnings);
            engine.AssertLogContains("MSB3246");

            // There should have been no ugly callstack dumped
            engine.AssertLogDoesntContain("Microsoft.Build.UnitTests");

            // But it should contain the message from the BadImageFormatException, something like
            //     WARNING MSB3246: Resolved file has a bad image, no metadata, or is otherwise inaccessible. The format of the file 'C:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion\BadImage.dll' is invalid
            engine.AssertLogContains("'C:\\WINNT\\Microsoft.NET\\Framework\\v2.0.MyVersion\\BadImage.dll'"); // just search for the un-localized part
        }

        /// <summary>
        /// Given a reference that resolves to a bad image, we should get a message, no warning and
        /// no reference. We don't want an exception.
        /// </summary>
        [Fact]
        public void ResolveBadImageInSecondary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(true);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsOnBadImage")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress563286",
                @"c:\WINNT\Microsoft.NET\Framework\v2.0.MyVersion"
            };
            Execute(t);

            // There should be one resolved file, because the dependency was bad.
            Assert.Single(t.ResolvedFiles);

            // There should be no related files.
            Assert.Empty(t.RelatedFiles);
            engine.AssertLogDoesntContain("BadImage.pdb");
            engine.AssertLogDoesntContain("HRESULT");

            // There should have been no warning about the exception because it's only a dependency
            Assert.Equal(0, engine.Warnings);

            // There should have been no ugly callstack dumped
            engine.AssertLogDoesntContain("Microsoft.Build.UnitTests");
        }

        /// <summary>
        /// Test the case where the search path, earlier on, contains an assembly that almost matches
        /// but the PKT is wrong.
        /// </summary>
        [Fact]
        public void ResolveReferenceThatHasWrongPKTInEarlierAssembly()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem(AssemblyRef.SystemData) };
            t.SearchPaths = new string[]
            {
                s_myProjectPath,
                s_myVersion20Path
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Data.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// FX assemblies should not be CopyLocal.
        /// </summary>
        [Fact]
        public void PrimaryFXAssemblyRefIsNotCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] { new TaskItem(AssemblyRef.SystemData) };
            t.SearchPaths = new string[]
            {
                s_myVersion20Path
            };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.Data.dll"), t.ResolvedFiles[0].ItemSpec);
            Assert.Equal("false", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If an item is explicitly Private=='true' (as opposed to implicitly when the attribute isn't set at all)
        /// then it should be CopyLocal true even if its in the FX directory
        /// </summary>
        [Fact]
        public void PrivateItemInFrameworksGetsCopyLocalTrue()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Create the mocks.
            Microsoft.Build.Shared.FileExists fileExists = new Microsoft.Build.Shared.FileExists(FileExists);
            Microsoft.Build.Shared.DirectoryExists directoryExists = new Microsoft.Build.Shared.DirectoryExists(DirectoryExists);
            Microsoft.Build.Tasks.GetDirectories getDirectories = new Microsoft.Build.Tasks.GetDirectories(GetDirectories);
            Microsoft.Build.Tasks.GetAssemblyName getAssemblyName = new Microsoft.Build.Tasks.GetAssemblyName(GetAssemblyName);
            Microsoft.Build.Tasks.GetAssemblyMetadata getAssemblyMetadata = new Microsoft.Build.Tasks.GetAssemblyMetadata(GetAssemblyMetadata);

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            };

            assemblyNames[0].SetMetadata("Private", "true"); // Fx file, but user chose private=true.

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            Execute(t);
            Assert.Equal(@"true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If we have no framework directories passed in and an assembly is found outside of the GAC then it should be able to be copy local.
        /// </summary>
        [Fact]
        public void NoFrameworkDirectoriesStillCopyLocal()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem(s_assemblyFolder_SomeAssemblyDllPath)
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = Array.Empty<string>();
            t.SearchPaths = new string[] { "{RawFileName}" };
            Execute(t);
            Assert.Equal(@"true", t.ResolvedFiles[0].GetMetadata("CopyLocal"));
        }

        /// <summary>
        /// If an item has a bad value for a boolean attribute, report a nice error that indicates which attribute it was.
        /// </summary>
        [Fact]
        public void Regress284485_PrivateItemWithBogusValue()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // Also construct a set of assembly names to pass in.
            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
            };

            assemblyNames[0].SetMetadata("Private", "bogus"); // Fx file, but user chose private=true.

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.SearchPaths = DefaultPaths;
            Execute(t);

            string message = String.Format(AssemblyResources.GetString("General.InvalidAttributeMetadata"), assemblyNames[0].ItemSpec, "Private", "bogus", "bool");
            Assert.Contains(message, engine.Log);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   Primary References
        ///         C
        ///         A version 2
        ///         And both A version 2 and C are CopyLocal=true
        ///   References - C
        ///        Depends on A version 1
        ///        Depends on B
        ///   References - B
        ///        Depends on A version 2
        ///
        ///
        /// Expect to have some information indicating that C and B depend on two different versions of A and that the primary reference which caused the problems
        /// are A and C.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesRegress444809()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null"), new TaskItem("C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null")
            };

            t.SearchPaths = new string[] {
                s_regress444809RootPath, s_regress444809_V2RootPath
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);
            ResourceManager resources = new ResourceManager("Microsoft.Build.Tasks.Strings", Assembly.GetExecutingAssembly());

            // Unresolved primary reference with itemspec "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null".
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null", s_regress444809_ADllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", s_regress444809_V2_ADllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", s_regress444809_CDllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", s_regress444809_BDllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", s_regress444809_V2_ADllPath);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   Primary References
        ///         A version 20 (Un Resolved)
        ///         B
        ///         D
        ///   References - B
        ///        Depends on A version 2
        ///   References - D
        ///        Depends on A version 20
        ///
        ///
        /// Expect to have some information indicating that Primary reference A, Reference B and Reference D conflict.
        /// </summary>
        [Fact]
        public void ConflictBetweenCopyLocalDependenciesRegress444809UnResolvedPrimaryReference()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[] {
                new TaskItem("A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null")
            };

            t.SearchPaths = new string[] {
                s_regress444809RootPath, s_regress444809_V2RootPath
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null", String.Empty);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.ReferenceDependsOn", "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", s_regress444809_V2_ADllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.UnResolvedPrimaryItemSpec", "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null");
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", s_regress444809_DDllPath);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.PrimarySourceItemsForReference", s_regress444809_BDllPath);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - B
        ///        Depends on D version 2
        ///   References - D, version 1
        ///
        /// Both D1 and D2 are CopyLocal. This is a warning because D1 is a lower version
        /// than both D2 so that can't unify. These means that eventually when
        /// they're copied to the output directory they'll conflict.
        /// </summary>
        [Fact]
        public void ConflictGeneratesMessageReferencingAssemblyName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(1, e.Warnings); // @"Expected one warning."

            // Check that we have a message identifying conflicts with "D"
            string warningMessage = e.WarningEvents[0].Message;
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.FoundConflicts", "D", string.Empty));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ConflictFound", "D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", "D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.FourSpaceIndent", ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ReferenceDependsOn", "D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", Path.Combine(s_myLibraries_V1Path, "D.dll"))));
        }

        [Fact]
        public void ConflictOutputsExtraInformationOnDemand()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };
            t.OutputUnresolvedAssemblyConflicts = true;

            Execute(t);

            ITaskItem[] conflicts = t.UnresolvedAssemblyConflicts;
            conflicts.Length.ShouldBe(1);
            conflicts[0].ItemSpec.ShouldBe("D");
            conflicts[0].GetMetadata("victorVersionNumber").ShouldBe("1.0.0.0");
            conflicts[0].GetMetadata("victimVersionNumber").ShouldBe("2.0.0.0");
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - B
        ///        Depends on D version 2
        ///        Depends on G, version 2
        ///   References - D, version 1
        ///   References - G, version 1
        ///
        /// All of Dv1, Dv2, Gv1 and Gv2 are CopyLocal. We should get two conflict warnings, one for D and one for G.
        /// </summary>
        [Fact]
        public void ConflictGeneratesMessageReferencingEachConflictingAssemblyName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
                new TaskItem("G, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(2, e.Warnings); // @"Expected two warnings."

            // Check that we have both the expected messages
            string warningMessage = e.WarningEvents[0].Message;
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.FoundConflicts", "D", string.Empty));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ConflictFound", "D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", "D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.FourSpaceIndent", ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ReferenceDependsOn", "D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", Path.Combine(s_myLibraries_V1Path, "D.dll"))));

            warningMessage = e.WarningEvents[1].Message;
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.FoundConflicts", "G", string.Empty));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ConflictFound", "G, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", "G, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"));
            warningMessage.ShouldContain(ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.FourSpaceIndent", ResourceUtilities.FormatResourceStringIgnoreCodeAndKeyword("ResolveAssemblyReference.ReferenceDependsOn", "G, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa", Path.Combine(s_myLibraries_V1Path, "G.dll"))));
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1
        ///   References - B
        ///        Depends on D version 2
        ///   References - D, version 2
        ///
        /// Both D1 and D2 are CopyLocal. This is not an error because D2 is a higher version
        /// than D1 so that can unify. D2 should be output as a Primary and D1 should be output
        /// as a dependency.
        /// </summary>
        [Fact]
        public void ConflictWithForeVersionPrimary()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[] {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.True(result); // @"Expected a success because this conflict is solvable."
            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V2_DDllPath));

            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V1_DDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V2_GDllPath));
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - D, version 1
        ///   References - D, version 2
        ///
        /// Both D1 and D2 are CopyLocal. This is an error because both D1 and D2 can't be copied to
        /// the output directory.
        /// </summary>
        [Fact]
        public void ConflictBetweenBackAndForeVersionsCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Equal(2, e.Warnings); // @"Expected a warning because this is an unresolvable conflict."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V2_DDllPath)); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - D, version 1
        ///   References - D, version 2
        ///
        /// Neither D1 nor D2 are CopyLocal. This is a solvable conflict because D2 has a higher version
        /// than D1 and there won't be an output directory conflict.
        /// </summary>
        [Fact]
        public void ConflictBetweenBackAndForeVersionsNotCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[] {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            bool result = Execute(t);

            Assert.True(result); // @"Expected success because this conflict is solvable."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V2_DDllPath)); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1, PKT=XXXX
        ///   References - C
        ///        Depends on D version 1, PKT=YYYY
        ///
        /// We can't tell which should win because the PKTs are different. This should be an error.
        /// </summary>
        [Fact]
        public void ConflictingDependenciesWithNonMatchingNames()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("C")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V1Path, @"c:\RogueLibraries\v1"
            };

            bool result = Execute(t);
            Assert.True(result); // "Execute should have failed because of insoluble conflict."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A
        ///        Depends on D version 1, PKT=XXXX
        ///   References - C
        ///        Depends on D version 1, PKT=YYYY
        ///   References - D version 1, PKT=XXXX
        ///
        /// D, PKT=XXXX should win because its referenced in the project.
        ///
        /// </summary>
        [Fact]
        public void ConflictingDependenciesWithNonMatchingNamesAndHardReferenceInProject()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("C"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V1Path, @"c:\RogueLibraries\v1"
            };

            Execute(t);

            Assert.Equal(3, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// A reference with a bogus version is provided. However, the user has chosen
        /// SpecificVersion='false' so we match the first one we come across.
        /// </summary>
        [Fact]
        public void SpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.Equal(Path.Combine(s_myVersion20Path, "System.XML.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// A reference with a bogus version is provided and the user has chosen SpecificVersion=true.
        /// In this case, since there is no specific version that can be matched, no reference is returned.
        /// </summary>
        [Fact]
        public void SpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "true");

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// A reference with a bogus version is provided and the user has left off SpecificVersion.
        /// In this case assume SpecificVersion=true implicitly.
        /// </summary>
        [Fact]
        public void SpecificVersionAbsent()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[] {
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Unresolved primary references should result in warnings.
        /// </summary>
        [Fact]
        public void Regress199998()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine m = new MockEngine(_output);
            t.BuildEngine = m;

            t.Assemblies = new ITaskItem[]
            {
                // An assembly that is unresolvable because it doesn't exist.
                new TaskItem(@"System.XML, Version=9.9.9999.9, Culture=neutral, PublicKeyToken=abababababababab")
            };

            t.SearchPaths = DefaultPaths;
            Execute(t);

            Assert.Empty(t.ResolvedFiles);
            // One warning for the un-resolved reference and one warning saying you are trying to target an assembly higher than the current target
            // framework.
            Assert.Equal(1, m.Warnings);
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has an <ExecutableExtension>.exe</ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// Expected:
        /// - The resulting assembly returned should be a.exe
        /// Rationale:
        /// The user browsed to an .exe, so that's what we should give them.
        /// </summary>
        [Fact]
        public void ExecutableExtensionEXE()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("ExecutableExtension", ".eXe");

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath,
                @"c:\MyExecutableLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyExecutableLibraries\a.exe")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has an <ExecutableExtension>.dll</ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// Expected:
        /// - The resulting assembly returned should be a.dll
        /// Rationale:
        /// The user browsed to a .dll, so that's what we should give them.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDLL()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("ExecutableExtension", ".DlL");

            t.SearchPaths = new string[]
            {
                @"c:\MyExecutableLibraries",
                s_myLibrariesRootPath
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_ADllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has no <ExecutableExtension></ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// - A.dll is first in the search order.
        /// Expected:
        /// - The resulting assembly returned should be a.dll
        /// Rationale:
        /// Without an ExecutableExtension the first assembly out of .dll,.exe wins.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDefaultDLLFirst()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath,
                @"c:\MyExecutableLibraries"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_ADllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has no <ExecutableExtension></ExecutableExtension> tag.
        /// - Both a.exe and a.dll exist on disk.
        /// - A.exe is first in the search order.
        /// Expected:
        /// - The resulting assembly returned should be a.exe
        /// Rationale:
        /// Without an ExecutableExtension the first assembly out of .dll,.exe wins.
        /// </summary>
        [Fact]
        public void ExecutableExtensionDefaultEXEFirst()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyExecutableLibraries",
                s_myLibrariesRootPath
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyExecutableLibraries\A.exe")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has <SpecificVersion>true</SpecificVersion> tag.
        /// - An assembly with a strong fusion name "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" exists first in the search order.
        /// - An assembly with a weak fusion name "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" is second in the search order.
        /// Expected:
        /// - This is an unresolved reference.
        /// Rationale:
        /// If specific version is true, but the reference is a simple name like "A", then there is no way to do a specific version match.
        /// This is a corner case. Other solutions that might have been just as good:
        /// - Fall back to SpecificVersion=false behavior.
        /// - Only match "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null". Note that all of our default VS projects have at least a version number.
        /// </summary>
        [Fact]
        public void SimpleNameWithSpecificVersionTrue()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "true");

            t.SearchPaths = new string[]
            {
                @"c:\MyStronglyNamed",
                s_myLibrariesRootPath
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to simple name "A".
        /// - The reference has <SpecificVersion>true</SpecificVersion> tag.
        /// - An assembly with a strong fusion name "A, PKT=..., Version=..., Culture=..." exists first in the search order.
        /// - An assembly with a weak fusion name "A, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null" is second in the search order.
        /// Expected:
        /// - The resulting assembly returned should be the strongly named a.dll.
        /// Rationale:
        /// If specific version is false, then we should match the first "A" that we find.
        /// </summary>
        [Fact]
        public void SimpleNameWithSpecificVersionFalse()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"c:\MyStronglyNamed",
                s_myLibrariesRootPath
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\MyStronglyNamed\A.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Consider this situation:
        ///
        /// App
        ///   References - D, version 1, IrreleventKeyValue=poo.
        ///
        /// There's plenty of junk that might end up in a fusion name that have nothing to do with
        /// assembly resolution. Make sure we can tolerate this for primary references.
        /// </summary>
        [Fact]
        public void IrrelevantAssemblyNameElement()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa, IrreleventKeyValue=poo"),
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path
            };

            t.TargetFrameworkDirectories = new string[] { s_myVersion20Path };

            bool result = Execute(t);

            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_V1_DDllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Regress EVERETT QFE 626
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A (Private=undefined)
        ///        Depends on D
        ///             Depends on E
        ///   References - D (Private=false)
        ///
        /// - Reference A does not have a Private attribute, but resolves to CopyLocal=true.
        /// - Reference D has explicit Private=false.
        /// - D would normally be CopyLocal=true.
        /// - E would normally be CopyLocal=true.
        ///
        /// Expected:
        /// - D should be CopyLocal=false because the of the matching Reference D which has explicit private=false.
        /// - E should be CopyLocal=false because it's a dependency of D which has explicit private=false.
        ///
        /// Rationale:
        /// This is QFE 626. If the user has set "Copy Local" to "false" in VS (means Private=false)
        /// then even if this turns out to be a dependency too, we still shouldn't copy.
        ///
        /// </summary>
        [Fact]
        public void RegressQFE626()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine engine = new MockEngine(_output);
            t.BuildEngine = engine;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("D")
            };
            t.Assemblies[1].SetMetadata("Private", "false");

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V1Path, s_myLibraries_V1_EPath
            };
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Single(t.ResolvedDependencyFiles); // Not 2 because D is treated as a primary reference.
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V1_E_EDllPath)); // "Expected to find assembly, but didn't."
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(0, engine.Errors);

            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (String.Equals(item.ItemSpec, s_myLibraries_V1_E_EDllPath, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("false", item.GetMetadata("CopyLocal"));
                }
            }
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   References - A (private=false)
        ///        Depends on D v1
        ///             Depends on E
        ///   References - B (private=true)
        ///        Depends on D v2
        ///             Depends on E
        ///
        /// Reference A is explicitly Private=false.
        /// Reference B is explicitly Private=true.
        /// Dependencies D and E would normally be CopyLocal=true.
        ///
        /// Expected:
        /// - D will be CopyLocal=false because it's dependency of A, which is private=false.
        /// - E will be CopyLocal=true because all source primary references aren't private=false.
        ///
        /// Rationale:
        /// Dependencies will be CopyLocal=false if all source primary references are Private=false.
        ///
        /// </summary>
        [Fact]
        public void Regress265054()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };
            t.Assemblies[0].SetMetadata("Private", "false");
            t.Assemblies[1].SetMetadata("Private", "true");

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V1Path, s_myLibraries_V2Path, s_myLibraries_V1_EPath
            };
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Equal(2, t.ResolvedFiles.Length);

            Assert.Equal(4, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V1_DDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V2_DDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V2_GDllPath));
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, s_myLibraries_V1_E_EDllPath));

            foreach (ITaskItem item in t.ResolvedDependencyFiles)
            {
                if (String.Equals(item.ItemSpec, s_myLibraries_V1_DDllPath, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("false", item.GetMetadata("CopyLocal"));
                }

                if (String.Equals(item.ItemSpec, s_myLibraries_V1_E_EDllPath, StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Equal("true", item.GetMetadata("CopyLocal"));
                }
            }
        }

        /// <summary>
        /// Here's how you get into this situation:
        ///
        /// App
        ///   References - A
        ///   References - B
        ///        Depends on A
        ///
        ///    And, the following conditions.
        ///     Primary "A" has no explicit Version (i.e. it's a simple name)
        ///        Primary "A" *is not* resolved.
        ///        Dependency "A" *is* resolved.
        ///
        /// Expected result:
        /// * No exceptions.
        /// * Build error about unresolved primary reference.
        ///
        /// </summary>
        [Fact]
        public void Regress312873_UnresolvedPrimaryWithResolveDependency()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B"),

                // We need a one more "A" because the bug was in a Compare function
                // called by .Sort. We need enough items to guarantee that A with null version
                // will be on the left side of a compare.
                new TaskItem("A")
};

            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress312873\b.dll");
            t.Assemblies[2].SetMetadata("HintPath", @"C:\Regress312873-2\a.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);
        }

        /// <summary>
        /// We weren't handling scatter assemblies.
        ///
        /// App
        ///   References - A
        ///
        ///    And, the following conditions.
        ///     Primary "A" has two scatter files "M1" and "M2"
        ///
        /// Expected result:
        /// * M1 and M2 should be output in ScatterFiles and CopyLocal.
        ///
        /// </summary>
        [Fact]
        public void Regress275161_ScatterAssemblies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress275161\a.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.True(ContainsItem(t.ScatterFiles, @"C:\Regress275161\m1.netmodule")); // "Expected to find scatter file m1."

            Assert.True(ContainsItem(t.ScatterFiles, @"C:\Regress275161\m2.netmodule")); // "Expected to find scatter file m2."

            Assert.True(ContainsItem(t.CopyLocalFiles, @"C:\Regress275161\m1.netmodule")); // "Expected to find scatter file m1 in CopyLocalFiles."

            Assert.True(ContainsItem(t.CopyLocalFiles, @"C:\Regress275161\m2.netmodule")); // "Expected to find scatter file m2 in CopyLocalFiles."
        }

        /// <summary>
        /// We weren't handling scatter assemblies.
        ///
        /// App
        ///   References - A
        ///        Depends on B v1.0.0.0
        ///   References - B v2.0.0.0
        ///
        ///
        ///    And, the following conditions.
        ///    * All assemblies are resolved.
        /// * All assemblies are CopyLocal=true.
        /// * Notice the conflict between versions of B.
        ///
        /// Expected result:
        /// * During conflict resolution, B v2.0.0.0 should win.
        /// * B v1.0.0.0 should still be listed in dependencies (there's not a strong case for this either way)
        /// * B v1.0.0.0 should be CopyLocal='false'
        ///
        /// </summary>
        [Fact]
        public void Regress317975_LeftoverLowerVersion()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
};

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress317975\a.dll");
            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress317975\v2\b.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            foreach (ITaskItem i in t.ResolvedDependencyFiles)
            {
                Assert.Equal(0, String.Compare(i.GetMetadata("CopyLocal"), "false", StringComparison.OrdinalIgnoreCase));
            }

            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"C:\Regress317975\B.dll")); // "Expected to find lower version listed in dependencies."
        }

        /// <summary>
        /// Mscorlib is special in that it doesn't always have complete metadata. For example,
        /// GetAssemblyName can return null. This was confusing the {RawFileName} resolution path,
        /// which is fairly different from the other code paths.
        ///
        /// App
        ///   References - "c:\path-to-mscorlib\mscorlib.dll" (Current FX)
        ///
        /// Expected result:
        /// * Even though mscorlib.dll doesn't have an assembly name, we should be able to return
        ///   a result.
        ///
        /// NOTES:
        /// * This test works because path-to-mscorlib is the same as the path to the FX folder.
        ///   Because of this, the hard-cache is used rather than actually calling GetAssemblyName
        ///   on mscorlib.dll. This isn't going to work in cases where mscorlib is from an FX other
        ///   than the current target. See the Part2 for a test that covers this other case.
        ///
        /// </summary>
        [Fact]
        public void Regress313086_Part1_MscorlibAsRawFilename()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(typeof(object).Module.FullyQualifiedName.ToLower())
};

            t.SearchPaths = new string[]
            {
                @"{RawFileName}"
            };

            t.TargetFrameworkDirectories = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName) };

            t.Execute();

            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Mscorlib is special in that it doesn't always have complete metadata. For example,
        /// GetAssemblyName can return null. This was confusing the {RawFileName} resolution path,
        /// which is fairly different from the other code paths.
        ///
        /// App
        ///   References - "c:\path-to-mscorlib\mscorlib.dll" (non-Current FX)
        ///
        /// Expected result:
        /// * Even though mscorlib.dll doesn't have an assembly name, we should be able to return
        ///   a result.
        ///
        /// NOTES:
        /// * This test is covering the case where mscorlib.dll is coming from somewhere besides
        ///   the main (ie Whidbey) FX.
        ///
        /// </summary>
        [Fact]
        public void Regress313086_Part2_MscorlibAsRawFilename()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"c:\Regress313086\mscorlib.dll")
};

            t.SearchPaths = new string[]
            {
                @"{RawFileName}"
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };

            Execute(t);

            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// If a directory path is passed into AssemblyFiles, then we should warn and continue on.
        /// </summary>
        [Fact]
        public void Regress284466_DirectoryIntoAssemblyFiles()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(s_unifyMeDll_V10Path),
                        new TaskItem(Path.GetTempPath())
                    };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.SearchPaths = DefaultPaths;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);
            Assert.Contains(
                String.Format(AssemblyResources.GetString("General.ExpectedFileGotDirectory"), Path.GetTempPath()),
                engine.Log);
        }

        /// <summary>
        /// If a relative assemblyFile is passed in resolve it as a full path.
        /// </summary>
        [Fact]
        public void RelativeAssemblyFiles()
        {
            string testPath = Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles");
            string previousCurrentDirectory = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(testPath);
            Directory.SetCurrentDirectory(testPath);
            try
            {
                // Create the engine.
                MockEngine engine = new MockEngine(_output);

                ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(@"..\RelativeAssemblyFiles\System.Xml.dll")
                    };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Single(t.ResolvedFiles);
                Assert.Equal(Path.Combine(testPath, "System.Xml.dll"), t.ResolvedFiles[0].ItemSpec);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousCurrentDirectory);

                if (Directory.Exists(testPath))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(testPath);
                }
            }
        }

        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryReferenceIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"c:\MyInaccessible"
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// In this case, the file is still resolved because it was passed in directly.
        /// There's no way to determine dependencies however.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryFileIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(@"c:\MyInaccessible\A.dll")
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Behave gracefully if a referenced assembly is inaccessible to the user.
        /// </summary>
        [Fact]
        public void Regress316906_UnauthorizedAccessViolation_PrimaryAsRawFileIsInaccessible()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem(@"c:\MyInaccessible\A.dll")
            };
            t.SearchPaths = new string[] { "{RawFileName}" };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// If there's a SearhPath like {Registry:,,} then still behave nicely.
        /// </summary>
        [Fact]
        public void Regress269704_MissingRegistryElements()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata("SpecificVersion", "false");

            t.SearchPaths = new string[]
            {
                @"{Registry:,,}",
                @"c:\MyAssemblyDoesntExistHere"
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// 1.  Create a C# classlibrary, and build it.
        /// 2.  Go to disk, and rename ClassLibrary1.dll (or whatever it was) to Foo.dll
        /// 3.  Create a C# console application.
        /// 4.  In the console app, add a File reference to Foo.dll.
        /// 5.  Build the console app.
        ///
        /// RESULTS (before bugfix):
        /// ========================
        /// MSBUILD : warning : Couldn't resolve this reference.  Could not locate assembly "ClassLibrary1"
        ///
        /// EXPECTED (after bugfix):
        /// ========================
        /// We think it might be reasonable for the ResolveAssemblyReference task to correctly resolve
        /// this reference, especially given the fact that the HintPath was provided in the project file.
        /// </summary>
        [Fact]
        public void Regress276548_AssemblyNameDifferentThanFusionName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };
            t.Assemblies[0].SetMetadata(
                "HintPath",
                @"c:\MyNameMismatch\Foo.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// When very long paths are passed in we should be robust.
        /// </summary>
        [Fact]
        public void Regress314573_VeryLongPaths()
        {
            string veryLongPath = @"C:\" + new String('a', 260);
            string veryLongFile = veryLongPath + "\\A.dll";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")                    // Resolved by HintPath
            };
            t.Assemblies[0].SetMetadata(
                "HintPath",
                veryLongFile);

            t.SearchPaths = new string[]
            {
                "{HintPathFromItem}"
            };

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(veryLongFile)            // Resolved as File Reference
            };

            Execute(t);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario." // Couldn't find dependencies for {HintPathFromItem}-resolved item.
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);  // This test used to have 1 here. But that was because the mock GetAssemblyName was not accurately throwing an exception for non-existent files.
        }

        /// <summary>
        /// Need to be robust in the face of assembly names with special characters.
        /// </summary>
        [Fact]
        public void Regress265003_EscapedCharactersInFusionName()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("\\=A\\=, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089"), // Characters that should be escaped in fusion names: \ , " ' =
                new TaskItem("__\\'ASP\\'dw0024ry")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");    // Important to this bug.
            t.Assemblies[1].SetMetadata("HintPath", @"c:\MyEscapedName\__'ASP'dw0024ry.dll");
            t.TargetFrameworkDirectories = new string[] { Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName) };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
                @"{HintPathFromItem}",
                @"c:\MyEscapedName"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "One warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
        }

        /// <summary>
        /// If we're given bogus Include (one with characters that would normally need escaping) but we also
        /// have a hintpath, then go ahead and resolve anyway because we know what the path should be.
        /// </summary>
        [Fact]
        public void Regress284081_UnescapedCharactersInFusionNameWithHintPath()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("__'ASP'dw0024ry")    // Would normally require quoting for the tick marks.
            };

            t.Assemblies[0].SetMetadata("HintPath", @"c:\MyEscapedName\__'ASP'dw0024ry.dll");

            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                s_myVersion20Path,
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Everett supported assembly names that had .dll at the end.
        /// </summary>
        [Fact]
        public void Regress366322_ReferencesWithFileExtensions()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A.dll")       // User really meant a fusion name here.
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_ADllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Support for multiple framework directories.
        /// </summary>
        [Fact]
        public void Regress366814_MultipleFrameworksFolders()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.TargetFrameworkDirectories = new string[] { @"c:\boguslocation", s_myLibrariesRootPath };
            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
            };

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_myLibraries_ADllPath)); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// If the App.Config file has a bad .XML then handle it gracefully.
        /// (i.e. no exception is thrown from the task.
        /// </summary>
        [Fact]
        public void Regress271273_BogusAppConfig()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
                    {
                        new TaskItem(s_unifyMeDll_V10Path)
                    };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                "        <dependentAssembly\n" +        // Intentionally didn't close this XML tag.
                "        </dependentAssembly>\n");

            try
            {
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                Execute(t);
            }
            finally
            {
                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        /// <summary>
        /// The user might pass in a HintPath that has a trailing slash. Need to not crash.
        ///
        /// </summary>
        [Fact]
        public void Regress354669_HintPathWithTrailingSlash()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress354669\");

            t.SearchPaths = new string[]
            {
                "{RawFileName}",
                "{CandidateAssemblyFiles}",
                s_myProjectPath,
                s_myVersion20Path,
                @"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}",
                "{AssemblyFolders}",
                "{HintPathFromItem}"
            };
            Execute(t);
        }

        /// <summary>
        /// The user might pass in a HintPath that has a trailing slash. Need to not crash.
        ///
        ///    Assembly A
        ///     References: C, version 2
        ///
        ///    Assembly B
        ///     References: C, version 1
        ///
        /// There is an App.Config file that redirects all versions of C to V2.
        /// Assemblies A and B are both located via their HintPath.
        ///
        /// </summary>
        [Fact]
        public void Regress339786_CrossVersionsWithAppConfig()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("B"),
                new TaskItem("A"),
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress339786\FolderB\B.dll");
            t.Assemblies[1].SetMetadata("HintPath", @"C:\Regress339786\FolderA\A.dll");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
            "        <dependentAssembly>\n" +
            "            <assemblyIdentity name='C' PublicKeyToken='null' culture='neutral' />\n" +
            "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
            "        </dependentAssembly>\n");
            t.AppConfigFile = appConfigFile;

            try
            {
                t.SearchPaths = new string[]
                {
                    "{HintPathFromItem}"
                };
                Execute(t);
            }
            finally
            {
                File.Delete(appConfigFile);
            }

            Assert.Single(t.ResolvedDependencyFiles);
        }

        /// <summary>
        /// An older LKG of the CLR could throw a FileLoadException if it doesn't recognize
        /// the assembly. We need to support this for dogfooding purposes.
        /// </summary>
        [Fact]
        public void Regress_DogfoodCLRThrowsFileLoadException()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("DependsMyFileLoadExceptionAssembly")
            };

            t.SearchPaths = new string[]
            {
                @"c:\OldClrBug"
            };
            Execute(t);
        }

        /// <summary>
        /// There was a bug in which any file mentioned in the InstalledAssemblyTables was automatically
        /// considered to be a file present in the framework directory. This assumption was originally true,
        /// but became false when Crystal Reports started putting their assemblies in this table.
        /// </summary>
        [Fact]
        public void Regress407623_RedistListDoesNotImplyPresenceInFrameworks()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("CrystalReportsAssembly")
            };

            t.Assemblies[0].SetMetadata("SpecificVersion", "false");    // Important to this bug.
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",        // Assembly is not here.
                @"c:\Regress407623"                    // Assembly is here.
            };

            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='CrystalReports-Redist' >" +
                        "<File AssemblyName='CrystalReportsAssembly' Version='2.0.3600.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >");

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Regress407623\CrystalReportsAssembly.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// If an invalid file name is passed to InstalledAssemblyTables we expect a warning even if no other redist lists are passed.
        /// </summary>
        [Fact]
        public void InvalidCharsInInstalledAssemblyTable()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("SomeAssembly")
            };

            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };
            t.InstalledAssemblyTables = new TaskItem[] { new TaskItem("asdfasdfasjr390rjfiogatg~~!@@##$%$%%^&**()") };

            Execute(t);
            e.AssertLogContains("MSB3250");
        }

        /// <summary>
        /// Here's how you get into this situation:
        ///
        /// App
        ///   References - Microsoft.Build.Engine
        ///     Hintpath = C:\Regress435487\microsoft.build.engine.dll
        ///
        ///    And, the following conditions.
        ///     microsoft.build.engine.dll has the redistlist InGac=true flag set.
        ///
        /// Expected result:
        /// * For the assembly to be CopyLocal=true
        ///
        /// </summary>
        [Fact]
        public void Regress435487_FxFileResolvedByHintPathShouldByCopyLocal()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"C:\Regress435487\microsoft.build.engine.dll");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}",
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            string redistFile = FileUtilities.GetTemporaryFileName();

            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='MyFancy-Redist' >" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >");

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal("true", t.ResolvedFiles[0].GetMetadata("CopyLocal")); // "Expected CopyLocal==true."
        }

        /// <summary>
        /// Verify when doing partial name matching with the assembly name that we also correctly do the partial name matching when trying to find
        /// assemblies from the redist list.
        /// </summary>
        [Fact]
        public void PartialNameMatchingFromRedist()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();

            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='MyFancy-Redist' >" +
                        // Simple name match where everything is the same except for version
                        "<File AssemblyName='A' Version='1.0.0.0' PublicKeyToken='a5d015c7d5a0b012' Culture='de-DE' FileVersion='2.0.40824.0' InGAC='true' />" +
                        "<File AssemblyName='A' Version='2.0.0.0' PublicKeyToken='a5d015c7d5a0b012' Culture='neutral' FileVersion='2.0.40824.0' InGAC='true' />" +
                        "<File AssemblyName='A' Version='3.0.0.0' PublicKeyToken='null' Culture='de-DE' FileVersion='2.0.40824.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyName v1 = new AssemblyName("A, Culture=de-DE, PublicKeyToken=a5d015c7d5a0b012, Version=1.0.0.0");
                AssemblyName v2 = new AssemblyName("A, Culture=Neutral, PublicKeyToken=a5d015c7d5a0b012, Version=2.0.0.0");
                AssemblyName v3 = new AssemblyName("A, Culture=de-DE, PublicKeyToken=null, Version=3.0.0.0");

                AssemblyNameExtension Av1 = new AssemblyNameExtension(v1);
                AssemblyNameExtension Av2 = new AssemblyNameExtension(v2);
                AssemblyNameExtension Av3 = new AssemblyNameExtension(v3);

                AssemblyTableInfo assemblyTableInfo = new AssemblyTableInfo(redistFile, "MyFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { assemblyTableInfo });
                InstalledAssemblies installedAssemblies = new InstalledAssemblies(redistList);

                AssemblyNameExtension assemblyName = new AssemblyNameExtension("A");
                AssemblyNameExtension foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.Equal(Av3, foundAssemblyName);

                assemblyName = new AssemblyNameExtension("A, PublicKeyToken=a5d015c7d5a0b012");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.Equal(Av2, foundAssemblyName);

                assemblyName = new AssemblyNameExtension("A, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.Equal(Av3, foundAssemblyName);

                assemblyName = new AssemblyNameExtension("A, PublicKeyToken=a5d015c7d5a0b012, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.Equal(Av1, foundAssemblyName);

                assemblyName = new AssemblyNameExtension("A, Version=17.0.0.0, PublicKeyToken=a5d015c7d5a0b012, Culture=de-DE");
                foundAssemblyName = FrameworkPathResolver.GetHighestVersionInRedist(installedAssemblies, assemblyName);
                Assert.Equal(assemblyName, foundAssemblyName);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        [Fact]
        public void Regress46599_BogusInGACValueForAssemblyInRedistList()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            string redistFile = CreateGenericRedistList();

            bool success = false;
            try
            {
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\Microsoft.Build.Engine.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\System.Xml.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistFile) };

                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
                File.Delete(redistFile);
            }

            Assert.True(success); // "Expected no errors."
            Assert.Equal(2, t.ResolvedFiles.Length); // "Expected two resolved assemblies."
        }

        [Fact]
        public void VerifyFrameworkFileMetadataFiles()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                // In framework directory and redist, should have metadata
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml"),
                // In framework directory, should have metadata
                new TaskItem("B"),
                // Not in framework directory but in redist, should have metadata
                new TaskItem("C"),
                // Not in framework directory and not in redist, should not have metadata
                new TaskItem("D")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}",
                @"c:\Somewhere\"
            };
            t.TargetFrameworkDirectories = new string[] { @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx" };

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;

            // Create a redist list which will contains both of the assemblies to search for
            string redistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                         "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                         "<File AssemblyName='C' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";

            string redistFile = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(redistFile, redistListContents);

            bool success = false;
            try
            {
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\Microsoft.Build.Engine.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\System.Xml.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\B.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"c:\somewhere\c.dll", StringComparison.OrdinalIgnoreCase) ||
                        String.Equals(path, @"c:\somewhere\d.dll", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });

                getAssemblyName = new GetAssemblyName(delegate (string path)
                {
                    if (String.Equals(path, @"r:\WINDOWS\Microsoft.NET\Framework\v2.0.myfx\B.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    if (String.Equals(path, @"c:\somewhere\d.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("D, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    return null;
                });
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistFile) };

                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
                File.Delete(redistFile);
            }

            Assert.True(success); // "Expected no errors."
            Assert.Equal(5, t.ResolvedFiles.Length); // "Expected two resolved assemblies."
            Assert.Equal("True", t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("Microsoft.Build.Engine", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile"), true);
            Assert.Equal("True", t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("System.Xml", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile"), true);
            Assert.Equal("True", t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("B", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile"), true);
            Assert.Equal("True", t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("C", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile"), true);
            Assert.Empty(t.ResolvedFiles.Where(Item => Item.GetMetadata("OriginalItemSpec").Equals("D", StringComparison.OrdinalIgnoreCase)).First().GetMetadata("FrameworkFile"));
        }

        /// <summary>
        /// Create a redist file which is used by many different tests
        /// </summary>
        /// <returns>Path to the redist list</returns>
        private static string CreateGenericRedistList()
        {
            // Create a redist list which will contains both of the assemblies to search for
            string redistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";

            string tempFile = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(tempFile, redistListContents);
            return tempFile;
        }

        [Fact]
        public void GetRedistListPathsFromDisk_ThrowsArgumentNullException()
        {
            bool caughtArgumentNullException = false;

            try
            {
                RedistList.GetRedistListPathsFromDisk(null);
            }
            catch (ArgumentNullException)
            {
                caughtArgumentNullException = true;
            }

            Assert.True(caughtArgumentNullException); // "Public method RedistList.GetRedistListPathsFromDisk should throw ArgumentNullException when its argument is null!"
        }

        /// <summary>
        /// Test the case where the redist list is empty and we pass in an empty set of allow lists
        /// We should return null as there is no point generating an allow list if there is nothing to subtract from.
        /// ResolveAssemblyReference will see this as null and log a warning indicating no redist assemblies were found therefore no deny list could be
        /// generated
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListEmptyAssemblyInfoNoRedistAssemblies()
        {
            RedistList redistList = RedistList.GetRedistList(Array.Empty<AssemblyTableInfo>());
            List<Exception> allowListErrors = new List<Exception>();
            List<string> allowListErrorFileNames = new List<string>();
            Dictionary<string, string> denyList = redistList.GenerateDenyList(Array.Empty<AssemblyTableInfo>(), allowListErrors, allowListErrorFileNames);
            Assert.Null(denyList); // "Should return null if the AssemblyTableInfo is empty and the redist list is empty"
        }

        /// <summary>
        /// Verify that when we go to generate a deny list but there were no subset list files passed in that we get NO deny list generated as there is nothing to subtract.
        /// Nothing meaning, we don't have any matching subset list files to say there are no good files.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListEmptyAssemblyInfoWithRedistAssemblies()
        {
            string redistFile = CreateGenericRedistList();
            try
            {
                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(Array.Empty<AssemblyTableInfo>(), allowListErrors, allowListErrorFileNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test the case where the subset lists cannot be read. The expectation is that the deny list will be empty as we have no proper allow lists to compare it to.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListNotFoundSubsetFiles()
        {
            string redistFile = CreateGenericRedistList();
            try
            {
                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();

                Dictionary<string, string> denyList = redistList.GenerateDenyList(
                                                                   new AssemblyTableInfo[]
                                                                                         {
                                                                                           new AssemblyTableInfo("c:\\RandomDirectory.xml", "TargetFrameworkDirectory"),
                                                                                           new AssemblyTableInfo("c:\\AnotherRandomDirectory.xml", "TargetFrameworkDirectory")
                                                                                          },
                                                                                          allowListErrors,
                                                                                          allowListErrorFileNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Equal(2, allowListErrors.Count); // "Expected there to be two errors in the allowListErrors, one for each missing file"
                Assert.Equal(2, allowListErrorFileNames.Count); // "Expected there to be two errors in the allowListErrorFileNames, one for each missing file"
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Test the case where there is random goo in the subsetList file. Expect the file to not be read in and a warning indicating the file was skipped due to a read error.
        /// This should also cause the allow list to be empty as the badly formatted file was the only allowlist subset file.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGarbageSubsetListFiles()
        {
            string redistFile = CreateGenericRedistList();
            string garbageSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    garbageSubsetFile,
                    "RandomGarbage, i am a bad file with random goo rather than anything important");

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(garbageSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Single(allowListErrors); // "Expected there to be an error in the allowListErrors"
                Assert.Single(allowListErrorFileNames); // "Expected there to be an error in the allowListErrorFileNames"
                Assert.DoesNotContain("MSB3257", ((Exception)allowListErrors[0]).Message); // "Expect to not have the null redist warning"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(garbageSubsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries and has a redist name
        ///     Subset list which has no redist name but has entries
        ///
        /// Expected:
        ///     Expect a warning that a redist list or subset list has no redist name.
        ///     There should be no deny list generated as no sub set lists were read in.
        ///
        /// Rational:
        ///     If we have no redist name to compare to the redist list redist name we cannot subtract the lists correctly.
        /// </summary>
        [Fact]
        public void RedistListNoSubsetListName()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                string subsetListContents =
                   "<FileList>" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // If the names do not match then i expect there to be no deny list items
                Assert.Empty(denyList); // "Expected to have no assembly in the deny list"
                Assert.Single(allowListErrors); // "Expected there to be one error in the allowListErrors"
                Assert.Single(allowListErrorFileNames); // "Expected there to be one error in the allowListErrorFileNames"
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.NoSubSetRedistListName", subsetFile);
                Assert.Contains(message, ((Exception)allowListErrors[0]).Message); // "Expected assertion to contain correct error code"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries but no redist name
        ///     Subset list which has a redist name and entries
        ///
        /// Expected:
        ///     Expect no deny list to be generated and no warnings to be emitted
        ///
        /// Rational:
        ///     Since the redist list name is null or empty we have no way of matching any subset list up to it.
        /// </summary>
        [Fact]
        public void RedistListNullkRedistListName()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            string subsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                string subsetListContents =
                   "<FileList Redist='MyRedistListFile'>" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                string redistListContents =
                  "<FileList>" +
                      "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >";
                File.WriteAllText(redistFile, redistListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // If the names do not match then i expect there to be no deny list items
                Assert.Empty(denyList); // "Expected to have no assembly in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no errors in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no errors in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Inputs:
        ///     Redist list which has entries and has a redist name
        ///     Subset list which has entries but has a different redist name than the redist list
        ///
        /// Expected:
        ///     There should be no deny list generated as no sub set lists with matching names were found.
        ///
        /// Rational:
        ///     If the redist name does not match then that subset list should not be subtracted from the redist list.
        ///     We only add assemblies to the deny list if there is a corosponding allow list even if it is empty to inform us what assemblies are good and which are not.
        /// </summary>
        [Fact]
        public void RedistListDifferentNameToSubSet()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                string subsetListContents =
                   "<FileList Redist='IAMREALLYREALLYDIFFERNT' >" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // If the names do not match then i expect there to be no deny list items
                Assert.Empty(denyList); // "Expected to have no assembly in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Test the case where the subset list has the same name as the redist list but it has no entries In this case
        /// the deny list should contain ALL redist list entries because there are no allow list files to remove from the deny list.
        /// </summary>
        [Fact]
        public void RedistListEmptySubsetMatchingName()
        {
            string redistFile = CreateGenericRedistList();
            string subsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                string subsetListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                   "</FileList >";
                File.WriteAllText(subsetFile, subsetListContents);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(subsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // If the names do not match then i expect there to be no deny list items
                Assert.Equal(2, denyList.Count); // "Expected to have two assembly in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"

                ArrayList allowListErrors2 = new ArrayList();
                ArrayList allowListErrorFileNames2 = new ArrayList();
                Dictionary<string, string> denyList2 = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);
                Assert.Same(denyList, denyList2);
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(subsetFile);
            }
        }

        /// <summary>
        /// Test the case where, no redist assemblies are read in.
        /// In this case no denylist can be generated.
        /// We should get a warning informing us that we could not create a deny list.
        /// </summary>
        [Fact]
        public void RedistListNoAssembliesinRedistList()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };

            string redistListPath = FileUtilities.GetTemporaryFileName();
            string subsetListPath = FileUtilities.GetTemporaryFileName();
            File.WriteAllText(subsetListPath, _xmlOnlySubset);
            try
            {
                File.WriteAllText(
                    redistListPath,
                   "RANDOMBOOOOOGOOGOOG");

                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(subsetListPath) };

                Execute(t);
                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoRedistAssembliesToGenerateExclusionList"));
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);
            }
        }

        /// <summary>
        /// Test the case where the subset list is a subset of the redist list. Make sure that
        /// even though there are two files in the redist list that only one shows up in the deny list.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGoodListsSubsetIsSubsetOfRedist()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                Assert.Single(denyList); // "Expected to have one assembly in the deny list"
                Assert.True(denyList.ContainsKey("System.Xml, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b03f5f7f11d50a3a")); // "Expected System.xml to be in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where we generate a deny list based on a set of subset file paths, and then ask for
        /// another deny list using the same file paths. We expect to get the exact same Dictionary out
        /// as it should be pulled from the cache.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListVerifyDenyListCache()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Single(denyList); // "Expected to have one assembly in the deny list"
                Assert.True(denyList.ContainsKey("System.Xml, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b03f5f7f11d50a3a")); // "Expected System.xml to be in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"

                List<Exception> allowListErrors2 = new List<Exception>();
                List<string> allowListErrorFileNames2 = new List<string>();
                Dictionary<string, string> denyList2 = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors2, allowListErrorFileNames2);
                Assert.Same(denyList, denyList2);
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the allow list and the redist list are identical
        /// In this case the deny list should be empty.
        ///
        /// We are also in a way testing the combining of subset files as we read in one assembly from two
        /// different subset lists while the redist list already contains both assemblies.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGoodListsSubsetIsSameAsRedistList()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            string goodSubsetFile2 = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineOnlySubset);
                File.WriteAllText(goodSubsetFile2, _xmlOnlySubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo2 = new AssemblyTableInfo(goodSubsetFile2, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });

                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo, subsetListInfo2 }, allowListErrors, allowListErrorFileNames);
                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the allow list is a superset of the redist list.
        /// This means there are more assemblies in the allow list than in the deny list.
        ///
        /// The deny list should be empty.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGoodListsSubsetIsSuperSet()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    goodSubsetFile,
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                       "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='false' />" +
                       "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                       "<File AssemblyName='System.Data' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                  "</FileList >");

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Check to see if comparing the assemblies in the redist list to the ones in the subset
        /// list are case sensitive or not, they should not be case sensitive.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGoodListsCheckCaseInsensitive()
        {
            string redistFile = CreateGenericRedistList();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(goodSubsetFile, _engineAndXmlSubset.ToUpperInvariant());

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFileNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFileNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFileNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Verify that when we go to generate a deny list but there were no subset list files passed in that we get NO deny list generated as there is nothing to subtract.
        /// Nothing meaning, we don't have any matching subset list files to say there are no good files.
        /// </summary>
        [Fact]
        public void RedistListGenerateDenyListGoodListsMultipleIdenticalAssembliesInRedistList()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            string goodSubsetFile = FileUtilities.GetTemporaryFileName();
            try
            {
                // Create a redist list which will contains both of the assemblies to search for
                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistFile, redistListContents);
                File.WriteAllText(goodSubsetFile, _engineAndXmlSubset);

                AssemblyTableInfo redistListInfo = new AssemblyTableInfo(redistFile, "TargetFrameworkDirectory");
                AssemblyTableInfo subsetListInfo = new AssemblyTableInfo(goodSubsetFile, "TargetFrameworkDirectory");
                RedistList redistList = RedistList.GetRedistList(new AssemblyTableInfo[] { redistListInfo });
                List<Exception> allowListErrors = new List<Exception>();
                List<string> allowListErrorFilesNames = new List<string>();
                Dictionary<string, string> denyList = redistList.GenerateDenyList(new AssemblyTableInfo[] { subsetListInfo }, allowListErrors, allowListErrorFilesNames);

                // Since there were no allow list expect the deny list to return null
                Assert.Empty(denyList); // "Expected to have no assemblies in the deny list"
                Assert.Empty(allowListErrors); // "Expected there to be no error in the allowListErrors"
                Assert.Empty(allowListErrorFilesNames); // "Expected there to be no error in the allowListErrorFileNames"
            }
            finally
            {
                File.Delete(redistFile);
                File.Delete(goodSubsetFile);
            }
        }

        /// <summary>
        /// Test the case where the framework directory is passed in as null
        /// </summary>
        [Fact]
        public void SubsetListFinderNullFrameworkDirectory()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                SubsetListFinder finder = new SubsetListFinder(Array.Empty<string>());
                finder.GetSubsetListPathsFromDisk(null);
            });
        }
        /// <summary>
        /// Test the case where the subsetsToSearchFor are passed in as null
        /// </summary>
        [Fact]
        public void SubsetListFinderNullSubsetToSearchFor()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                SubsetListFinder finder = new SubsetListFinder(null);
            });
        }
        /// <summary>
        /// Test the case where the subsetsToSearchFor are an empty array
        /// </summary>
        [Fact]
        public void SubsetListFinderEmptySubsetToSearchFor()
        {
            SubsetListFinder finder = new SubsetListFinder(Array.Empty<string>());
            string[] returnArray = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");
            Assert.Empty(returnArray); // "Expected the array returned to be 0 length"
        }

        /// <summary>
        /// Verify that the method will not crash if there are empty string array elements
        /// </summary>
        [Fact]
        public void SubsetListFinderVerifyEmptyInSubsetsToSearchFor()
        {
            // Verify the program will not crash when an empty string is passed in
            SubsetListFinder finder = new SubsetListFinder(new string[] { "Clent", string.Empty, "Bar" });
            string[] returnArray = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");
            string[] returnArray2 = finder.GetSubsetListPathsFromDisk("FrameworkDirectory");

            Assert.Empty(returnArray);
            Assert.Equal(returnArray.Length, returnArray2.Length);
        }

        /// <summary>
        /// Verify when we have valid subset files and their names are in the subsets to search for that we correctly find the files
        /// </summary>
        [Fact]
        public void SubsetListFinderSubsetExists()
        {
            string frameworkDirectory = Path.Combine(ObjectModelHelpers.TempProjectDir, "SubsetListsTestExists");
            string subsetDirectory = Path.Combine(frameworkDirectory, SubsetListFinder.SubsetListFolder);
            string clientXml = Path.Combine(subsetDirectory, "Client.xml");
            string fooXml = Path.Combine(subsetDirectory, "Foo.xml");

            try
            {
                Directory.CreateDirectory(subsetDirectory);
                File.WriteAllText(clientXml, "Random File Contents");
                File.WriteAllText(fooXml, "Random File Contents");
                SubsetListFinder finder = new SubsetListFinder(new string[] { "Client", "Foo" });
                string[] returnArray = finder.GetSubsetListPathsFromDisk(frameworkDirectory);
                Assert.Contains("Client.xml", returnArray[0]); // "Expected first element to contain Client.xml"
                Assert.Contains("Foo.xml", returnArray[1]); // "Expected first element to contain Foo.xml"
                Assert.Equal(2, returnArray.Length); // "Expected there to be two elements in the array"
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(frameworkDirectory, true);
            }
        }

        /// <summary>
        /// Verify that if there are files of the correct name but of the wrong extension that they are not found.
        /// </summary>
        [Fact]
        public void SubsetListFinderNullSubsetExistsButNotXml()
        {
            string frameworkDirectory = Path.Combine(ObjectModelHelpers.TempProjectDir, "SubsetListsTestExistsNotXml");
            string subsetDirectory = Path.Combine(frameworkDirectory, SubsetListFinder.SubsetListFolder);
            string clientXml = Path.Combine(subsetDirectory, "Clent.Notxml");
            string fooXml = Path.Combine(subsetDirectory, "Foo.Notxml");

            try
            {
                Directory.CreateDirectory(subsetDirectory);
                File.WriteAllText(clientXml, "Random File Contents");
                File.WriteAllText(fooXml, "Random File Contents");
                SubsetListFinder finder = new SubsetListFinder(new string[] { "Client", "Foo" });
                string[] returnArray = finder.GetSubsetListPathsFromDisk(frameworkDirectory);
                Assert.Empty(returnArray); // "Expected there to be two elements in the array"
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(frameworkDirectory, true);
            }
        }

        [Fact]
        public void IgnoreDefaultInstalledAssemblyTables()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

            t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
            t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };

            string implicitRedistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";
            string implicitRedistListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\RedistList\\ImplicitList.xml", implicitRedistListContents);
            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine");

            string explicitRedistListContents =
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                    "</FileList >";
            string explicitRedistListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\RedistList\\ExplicitList.xml", explicitRedistListContents);
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

            t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(explicitRedistListPath) };

            // Only the explicitly specified redist list should be used
            t.IgnoreDefaultInstalledAssemblyTables = true;

            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;

            fileExists = new FileExists(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            });

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }

            Assert.True(success); // "Expected no errors."
            Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
            Assert.Contains("System.Xml", t.ResolvedFiles[0].ItemSpec); // "Expected System.Xml to resolve."
        }

        /// <summary>
        /// A null deny list should be the same as an empty one.
        /// </summary>
        [Fact]
        public void ReferenceTableNullDenyList()
        {
            TaskLoggingHelper log = new TaskLoggingHelper(new ResolveAssemblyReference());
            ReferenceTable referenceTable = MakeEmptyReferenceTable(log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            table.Add(engineAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            referenceTable.MarkReferencesForExclusion(null);
            referenceTable.RemoveReferencesMarkedForExclusion(false, String.Empty);
            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected Dictionary to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the Dictionary"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
        }

        /// <summary>
        /// Test the case where the denylist is empty.
        /// </summary>
        [Fact]
        public void ReferenceTableEmptyDenyList()
        {
            TaskLoggingHelper log = new TaskLoggingHelper(new ResolveAssemblyReference());
            ReferenceTable referenceTable = MakeEmptyReferenceTable(log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            table.Add(engineAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            referenceTable.MarkReferencesForExclusion(new Dictionary<string, string>());
            referenceTable.RemoveReferencesMarkedForExclusion(false, String.Empty);
            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected Dictionary to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the Dictionary"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
        }

        /// <summary>
        /// Verify the case where there are primary references in the reference table which are also in the deny list
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInDenyList()
        {
            MockEngine mockEngine = new MockEngine(_output);
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            var denyList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            denyList[engineAssemblyName.FullName] = null;
            string[] targetFrameworks = new string[] { "Client", "Web" };
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null);

            referenceTable.MarkReferencesForExclusion(denyList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, subSetName);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected dictionary to be a different instance"
            Assert.Single(table2); // "Expected there to be one elements in the dictionary"
            Assert.False(table2.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            mockEngine.AssertLogContains(warningMessage);
        }

        /// <summary>
        /// Verify the case where there are primary references in the reference table which are also in the deny list
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInDenyListSpecificVersionTrue()
        {
            MockEngine mockEngine = new MockEngine(_output);
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            taskItem.SetMetadata("SpecificVersion", "true");
            reference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            var denyList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            denyList[engineAssemblyName.FullName] = null;
            string[] targetFrameworks = new string[] { "Client", "Web" };
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null);
            referenceTable.MarkReferencesForExclusion(denyList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, subSetName);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected dictionary to be a different instance"
            Assert.Equal(2, table2.Count); // "Expected there to be two elements in the dictionary"
            Assert.True(table2.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            mockEngine.AssertLogDoesntContain(warningMessage);
        }

        /// <summary>
        /// Verify the generation of the targetFrameworkSubSetName
        /// </summary>
        [Fact]
        public void TestGenerateFrameworkName()
        {
            string[] targetFrameworks = new string[] { "Client" };
            Assert.Equal("Client", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null));

            targetFrameworks = new string[] { "Client", "Framework" };
            Assert.Equal("Client, Framework", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null));

            targetFrameworks = Array.Empty<string>();
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null)));

            targetFrameworks = null;
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, null)));

            ITaskItem[] installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml") };
            Assert.Equal("Client", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable));

            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml"), new TaskItem("D:\\foo\\bar\\Framework.xml") };
            Assert.Equal("Client, Framework", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable));

            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Client.xml"), new TaskItem("D:\\foo\\bar\\Framework2\\"), new TaskItem("D:\\foo\\bar\\Framework"), new TaskItem("Nothing") };
            Assert.Equal("Client, Framework, Nothing", ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable));

            installedSubSetTable = Array.Empty<ITaskItem>();
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable)));

            installedSubSetTable = null;
            Assert.True(String.IsNullOrEmpty(ResolveAssemblyReference.GenerateSubSetName(null, installedSubSetTable)));

            targetFrameworks = new string[] { "Client", "Framework" };
            installedSubSetTable = new ITaskItem[] { new TaskItem("c:\\foo\\Mouse.xml"), new TaskItem("D:\\foo\\bar\\Man.xml") };
            Assert.Equal("Client, Framework, Mouse, Man", ResolveAssemblyReference.GenerateSubSetName(targetFrameworks, installedSubSetTable));
        }

        /// <summary>
        /// Verify the case where we just want to remove the references before conflict resolution and not print out the warning.
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryItemInDenyListRemoveOnlyNoWarn()
        {
            MockEngine mockEngine = new MockEngine(_output);
            ResolveAssemblyReference rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            ReferenceTable referenceTable = MakeEmptyReferenceTable(rar.Log);
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;

            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            Reference reference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            reference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            table.Add(engineAssemblyName, reference);
            table.Add(xmlAssemblyName, new Reference(isWinMDFile, fileExists, getRuntimeVersion));

            var denyList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            denyList[engineAssemblyName.FullName] = null;
            referenceTable.MarkReferencesForExclusion(denyList);
            referenceTable.RemoveReferencesMarkedForExclusion(true, String.Empty);

            Dictionary<AssemblyNameExtension, Reference> table2 = referenceTable.References;
            string subSetName = ResolveAssemblyReference.GenerateSubSetName(Array.Empty<string>(), null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem.ItemSpec, subSetName);
            Assert.False(Object.ReferenceEquals(table, table2)); // "Expected dictionary to be a different instance"
            Assert.Single(table2); // "Expected there to be one elements in the dictionary"
            Assert.False(table2.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.True(table2.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.True(String.IsNullOrEmpty(mockEngine.Log));
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference : sqlDependencyReference is in deny list
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);
            sqlDependencyReference.AddError(new Exception("CouldNotResolveSQLDependency"));
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakeDependentAssemblyReference(enginePrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);
            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage });
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference
        /// and enginePrimary->sqlDependencyReference: sqlDependencyReference is in deny list
        /// and systemxml->enginePrimary
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyList2()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);
            xmlPrimaryReference.MakeDependentAssemblyReference(enginePrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);
            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage });
        }

        /// <summary>
        /// Testing case  enginePrimary->XmlPrimary with XMLPrimary in the BL
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryToPrimaryDependencyWithOneInDenyList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            // Make engine depend on xml primary when xml primary is a primary reference as well
            xmlPrimaryReference.AddSourceItems(enginePrimaryReference.GetSourceItems());
            xmlPrimaryReference.AddDependee(enginePrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, null, null, xmlAssemblyName, enginePrimaryReference, null, null, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { xmlAssemblyName }, out denyList);
            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, xmlAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailedToResolveReferenceBecausePrimaryAssemblyInExclusionList", taskItem2.ItemSpec, subsetName);
            mockEngine.AssertLogContains(warningMessage);
            mockEngine.AssertLogContains(warningMessage2);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to not find the xmlAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
        }

        /// <summary>
        /// Testing case  enginePrimary->XmlPrimary->dataDependency with dataDependency in the BL
        /// </summary>
        [Fact]
        public void ReferenceTablePrimaryToPrimaryToDependencyWithOneInDenyList()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            Reference dataDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);

            TaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            // Make engine depend on xml primary when xml primary is a primary reference as well
            xmlPrimaryReference.AddSourceItems(enginePrimaryReference.GetSourceItems());
            xmlPrimaryReference.AddDependee(enginePrimaryReference);

            dataDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, null, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, null, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { dataAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);
            mockEngine.AssertLogContains(warningMessage);
            mockEngine.AssertLogContains(warningMessage2);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to not find the xmlAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to not find the engineAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(dataAssemblyName)); // "Expected to not find the dataAssemblyName in the referenceList"
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference
        /// and xmlPrimary->sqlDependencyReference: sqlDependencyReference is in deny list
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyList3()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage, warningMessage2 });
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference
        /// and xmlPrimary->dataDependencyReference: sqlDependencyReference is in deny list
        /// expect to see one dependency warning message
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyList4()
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, Array.Empty<string>(), null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null,
#if FEATURE_WIN32_REGISTRY
                null, null, null,
#endif
                null, null, null, new Version("4.0"), null, null, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false, null);
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(dataDependencyReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            VerifyReferenceTable(referenceTable, mockEngine, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, new string[] { warningMessage, warningMessage2 });
        }

        /// <summary>
        /// Testing case  enginePrimary -> dataDependencyReference->sqlDependencyReference
        /// enginePrimary -> dataDependencyReference
        /// xmlPrimaryReference ->DataDependency
        /// dataDependencyReference and sqlDependencyReference are in deny list
        /// expect to see two dependency warning messages in the enginePrimaryCase and one in the xmlPrimarycase
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyList5()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            ITaskItem taskItem2 = new TaskItem("System.Xml");
            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, false, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName, dataAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);
            string warningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string warningMessage3 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Empty(table); // "Expected there to be two elements in the dictionary"
            Assert.False(table.ContainsKey(sqlclientAssemblyName)); // "Expected to not find the sqlclientAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(dataAssemblyName)); // "Expected to not to find the dataAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.False(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"

            string[] warningMessages = new string[] { warningMessage, warningMessage2, warningMessage3 };
            foreach (string message in warningMessages)
            {
                Console.Out.WriteLine("WarningMessageToAssert:" + message);
                mockEngine.AssertLogContains(message);
            }
            table.Clear();
        }

        /// <summary>
        /// Testing case
        /// enginePrimary -> dataDependencyReference   also enginePrimary->sqlDependencyReference   specific version = true on the primary
        /// xmlPrimaryReference ->dataDependencyReference specific version = false on the primary
        /// dataDependencyReference and sqlDependencyReference is in the deny list.
        /// Expect to see one dependency warning messages xmlPrimarycase and no message for enginePrimary
        /// Also expect to resolve all files except for xmlPrimaryReference
        /// </summary>
        [Fact]
        public void ReferenceTableDependentItemsInDenyListPrimaryWithSpecificVersion()
        {
            ReferenceTable referenceTable;
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            Dictionary<string, string> denyList;
            AssemblyNameExtension engineAssemblyName = new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension dataAssemblyName = new AssemblyNameExtension("System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension sqlclientAssemblyName = new AssemblyNameExtension("System.SqlClient, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            AssemblyNameExtension xmlAssemblyName = new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            Reference enginePrimaryReference;
            Reference dataDependencyReference;
            Reference sqlDependencyReference;
            Reference xmlPrimaryReference;

            GenerateNewReferences(out enginePrimaryReference, out dataDependencyReference, out sqlDependencyReference, out xmlPrimaryReference);

            ITaskItem taskItem = new TaskItem("Microsoft.Build.Engine");
            taskItem.SetMetadata("SpecificVersion", "true");

            ITaskItem taskItem2 = new TaskItem("System.Xml");
            taskItem2.SetMetadata("SpecificVersion", "false");

            xmlPrimaryReference.MakePrimaryAssemblyReference(taskItem2, false, ".dll");
            enginePrimaryReference.MakePrimaryAssemblyReference(taskItem, true, ".dll");
            enginePrimaryReference.FullPath = "FullPath";
            xmlPrimaryReference.FullPath = "FullPath";
            dataDependencyReference.FullPath = "FullPath";
            sqlDependencyReference.FullPath = "FullPath";
            dataDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            sqlDependencyReference.MakeDependentAssemblyReference(enginePrimaryReference);
            dataDependencyReference.MakeDependentAssemblyReference(xmlPrimaryReference);

            InitializeMockEngine(out referenceTable, out mockEngine, out rar);
            AddReferencesToReferenceTable(referenceTable, engineAssemblyName, dataAssemblyName, sqlclientAssemblyName, xmlAssemblyName, enginePrimaryReference, dataDependencyReference, sqlDependencyReference, xmlPrimaryReference);

            InitializeExclusionList(referenceTable, new AssemblyNameExtension[] { sqlclientAssemblyName, dataAssemblyName }, out denyList);

            string subsetName = ResolveAssemblyReference.GenerateSubSetName(new string[] { "Client" }, null);
            string warningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem2.ItemSpec, dataAssemblyName.FullName, subsetName);
            string notExpectedwarningMessage = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, dataAssemblyName.FullName, subsetName);
            string notExpectedwarningMessage2 = rar.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", taskItem.ItemSpec, sqlclientAssemblyName.FullName, subsetName);

            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(3, table.Count); // "Expected there to be three elements in the dictionary"
            Assert.True(table.ContainsKey(sqlclientAssemblyName)); // "Expected to find the sqlclientAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(dataAssemblyName)); // "Expected to find the dataAssemblyName in the referenceList"
            Assert.False(table.ContainsKey(xmlAssemblyName)); // "Expected not to find the xmlssemblyName in the referenceList"
            Assert.True(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"

            string[] warningMessages = new string[] { warningMessage };
            foreach (string message in warningMessages)
            {
                Console.Out.WriteLine("WarningMessageToAssert:" + message);
                mockEngine.AssertLogContains(message);
            }

            mockEngine.AssertLogDoesntContain(notExpectedwarningMessage);
            mockEngine.AssertLogDoesntContain(notExpectedwarningMessage2);
            table.Clear();
        }

        private static ReferenceTable MakeEmptyReferenceTable(TaskLoggingHelper log)
        {
            ReferenceTable referenceTable = new ReferenceTable(null, false, false, false, false, Array.Empty<string>(), null, null, null, null, null, null, SystemProcessorArchitecture.None, fileExists, null, null, null, null,
#if FEATURE_WIN32_REGISTRY
                null, null, null,
#endif
                null, null, new Version("4.0"), null, log, null, true, false, null, null, false, null, WarnOrErrorOnTargetArchitectureMismatchBehavior.None, false, false, null);
            return referenceTable;
        }

        /// <summary>
        /// Verify the correct references are still in the references table and that references which are in the deny list are not in the references table
        /// Also verify any expected warning messages are seen in the log.
        /// </summary>
        private static void VerifyReferenceTable(ReferenceTable referenceTable, MockEngine mockEngine, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, string[] warningMessages)
        {
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Empty(table); // "Expected there to be zero elements in the dictionary"

            if (warningMessages != null)
            {
                foreach (string warningMessage in warningMessages)
                {
                    Console.Out.WriteLine("WarningMessageToAssert:" + warningMessages);
                    mockEngine.AssertLogContains(warningMessage);
                }
            }

            table.Clear();
        }

        /// <summary>
        /// Make sure we get an argument null exception when the profileName is set to null
        /// </summary>
        [Fact]
        public void TestProfileNameNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.ProfileName = null;
            });
        }
        /// <summary>
        /// Make sure we get an argument null exception when the ProfileFullFrameworkFolders is set to null
        /// </summary>
        [Fact]
        public void TestProfileFullFrameworkFoldersFoldersNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.FullFrameworkFolders = null;
            });
        }
        /// <summary>
        /// Make sure we get an argument null exception when the ProfileFullFrameworkAssemblyTables is set to null
        /// </summary>
        [Fact]
        public void TestProfileFullFrameworkAssemblyTablesNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference rar = new ResolveAssemblyReference();
                rar.FullFrameworkAssemblyTables = null;
            });
        }
        /// <summary>
        /// Verify that setting a subset and a profile at the same time will cause an error to be logged and rar to return false
        /// </summary>
        [Fact]
        public void TestProfileAndSubset1()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(_output, out mockEngine, out rar);

            rar.TargetFrameworkSubsets = new string[] { "Client" };
            rar.ProfileName = "Client";
            rar.FullFrameworkFolders = new string[] { "Client" };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.CannotSetProfileAndSubSet"));
        }

        /// <summary>
        /// Verify that setting a subset and a profile at the same time will cause an error to be logged and rar to return false
        /// </summary>
        [Fact]
        public void TestProfileAndSubset2()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(_output, out mockEngine, out rar);

            rar.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem("Client.xml") };
            rar.ProfileName = "Client";
            rar.FullFrameworkFolders = new string[] { "Client" };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.CannotSetProfileAndSubSet"));
        }

        /// <summary>
        /// Verify setting certain combinations of Profile parameters will case an error to be logged and rar to fail execution.
        ///
        /// Test the case where the profile name is not set and ProfileFullFrameworkFolders is set.
        /// </summary>
        [Fact]
        public void TestProfileParameterCombinations()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(_output, out mockEngine, out rar);
            rar.ProfileName = "Client";
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.MustSetProfileNameAndFolderLocations"));
        }

        /// <summary>
        /// Verify when the frameworkdirectory metadata is not set on the ProfileFullFrameworkAssemblyTables that an
        /// error is logged and rar fails.
        /// </summary>
        [Fact]
        public void TestFrameworkDirectoryMetadata()
        {
            MockEngine mockEngine;
            ResolveAssemblyReference rar;
            InitializeRARwithMockEngine(_output, out mockEngine, out rar);
            TaskItem item = new TaskItem("Client.xml");
            rar.ProfileName = "Client";
            rar.FullFrameworkAssemblyTables = new ITaskItem[] { item };
            Assert.False(rar.Execute());
            mockEngine.AssertLogContains(rar.Log.FormatResourceString("ResolveAssemblyReference.FrameworkDirectoryOnProfiles", item.ItemSpec));
        }

        private static void InitializeRARwithMockEngine(ITestOutputHelper output, out MockEngine mockEngine, out ResolveAssemblyReference rar)
        {
            mockEngine = new MockEngine(output);
            rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;
        }

        /// <summary>
        /// Add a set of references and their names to the reference table.
        /// </summary>
        private static void AddReferencesToReferenceTable(ReferenceTable referenceTable, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, Reference enginePrimaryReference, Reference dataDependencyReference, Reference sqlDependencyReference, Reference xmlPrimaryReference)
        {
            Dictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            if (enginePrimaryReference != null)
            {
                table.Add(engineAssemblyName, enginePrimaryReference);
            }

            if (dataDependencyReference != null)
            {
                table.Add(dataAssemblyName, dataDependencyReference);
            }
            if (sqlDependencyReference != null)
            {
                table.Add(sqlclientAssemblyName, sqlDependencyReference);
            }

            if (xmlPrimaryReference != null)
            {
                table.Add(xmlAssemblyName, xmlPrimaryReference);
            }
        }

        /// <summary>
        /// Initialize the mock engine so we can look at the warning messages, also put the assembly name which is to be in the deny list into the deny list.
        /// Call remove references so that we can then validate the results.
        /// </summary>
        private void InitializeMockEngine(out ReferenceTable referenceTable, out MockEngine mockEngine, out ResolveAssemblyReference rar)
        {
            mockEngine = new MockEngine(_output);
            rar = new ResolveAssemblyReference();
            rar.BuildEngine = mockEngine;

            referenceTable = MakeEmptyReferenceTable(rar.Log);
        }

        /// <summary>
        /// Initialize the deny list and use it to remove references from the reference table
        /// </summary>
        private void InitializeExclusionList(ReferenceTable referenceTable, AssemblyNameExtension[] assembliesForDenyList, out Dictionary<string, string> denyList)
        {
            denyList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AssemblyNameExtension assemblyName in assembliesForDenyList)
            {
                denyList[assemblyName.FullName] = null;
            }

            referenceTable.MarkReferencesForExclusion(denyList);
            referenceTable.RemoveReferencesMarkedForExclusion(false, "Client");
        }

        /// <summary>
        /// Before each test to validate the references are correctly removed from the reference table we need to make new instances of them
        /// </summary>
        /// <param name="enginePrimaryReference"></param>
        /// <param name="dataDependencyReference"></param>
        /// <param name="sqlDependencyReference"></param>
        /// <param name="xmlPrimaryReference"></param>
        private static void GenerateNewReferences(out Reference enginePrimaryReference, out Reference dataDependencyReference, out Reference sqlDependencyReference, out Reference xmlPrimaryReference)
        {
            enginePrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            dataDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            sqlDependencyReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
            xmlPrimaryReference = new Reference(isWinMDFile, fileExists, getRuntimeVersion);
        }

        /// <summary>
        /// This test will verify the IgnoreDefaultInstalledSubsetTables property on the RAR task.
        /// The property determines whether or not RAR will search the target framework directories under the subsetList folder for
        /// xml files matching the client subset names passed into the TargetFrameworkSubset property.
        ///
        /// The default for the property is false, when the value is false RAR will search the SubsetList folder under the TargetFramework directories
        /// for the xml files with names in the TargetFrameworkSubset property.  When the value is true, RAR will not search the SubsetList directory. The only
        /// way to specify a TargetFrameworkSubset is to pass one to the InstalledAssemblySubsetTables property.
        /// </summary>
        [Fact]
        public void IgnoreDefaultInstalledSubsetTables()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                string subsetListClientPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\Client.xml", _engineOnlySubset);
                string explicitSubsetListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\ExplicitList.xml", _xmlOnlySubset);
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(explicitSubsetListPath) };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);

                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
                Assert.Contains("System.Xml", t.ResolvedFiles[0].ItemSpec); // "Expected System.Xml to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Generate helper delegates for returning the file existence and the assembly name.
        /// Also run the rest and return the result.
        /// </summary>
        private bool GenerateHelperDelegatesAndExecuteTask(ResolveAssemblyReference t, string microsoftBuildEnginePath, string systemXmlPath)
        {
            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            fileExists = new FileExists(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            });

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }
            return success;
        }

        /// <summary>
        /// Test the case where there are no client subset names passed in but an InstalledDefaultSubsetTable
        /// is passed in. We expect to use that.
        /// </summary>
        [Fact]
        public void NoClientSubsetButInstalledSubTables()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                // Only the explicitly specified redist list should be used
                t.TargetFrameworkSubsets = Array.Empty<string>();

                // Create a subset list which should be read in
                string explicitSubsetListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                string explicitSubsetListPath = ObjectModelHelpers.CreateFileInTempProjectDirectory("v3.5\\SubsetList\\ExplicitList.xml", explicitSubsetListContents);
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(explicitSubsetListPath) };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);

                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
                Assert.Contains("System.Xml", t.ResolvedFiles[0].ItemSpec); // "Expected System.Xml to resolve."
                MockEngine engine = ((MockEngine)t.BuildEngine);
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.UsingExclusionList"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Verify the case where the installedSubsetTables are null
        /// </summary>
        [Fact]
        public void NullInstalledSubsetTables()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.InstalledAssemblySubsetTables = null;
            });
        }
        /// <summary>
        /// Verify the case where the targetFrameworkSubsets are null
        /// </summary>
        [Fact]
        public void NullTargetFrameworkSubsets()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.TargetFrameworkSubsets = null;
            });
        }
        /// <summary>
        /// Verify the case where the FulltargetFrameworkSubsetNames are null
        /// </summary>
        [Fact]
        public void NullFullTargetFrameworkSubsetNames()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ResolveAssemblyReference reference = new ResolveAssemblyReference();
                reference.FullTargetFrameworkSubsetNames = null;
            });
        }
        /// <summary>
        /// Test the case where a non existent subset list path is used and no additional subsets are passed in.
        /// </summary>
        [Fact]
        public void FakeSubsetListPathsNoAdditionalSubsets()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}" };

                t.TargetFrameworkSubsets = new string[] { "NOTTOEXIST" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };

                // Only the explicitly specified redist list should be used
                t.IgnoreDefaultInstalledAssemblyTables = true;

                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                MockEngine engine = ((MockEngine)t.BuildEngine);
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.UsingExclusionList"));
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoSubsetsFound"));
                Assert.Equal(2, t.ResolvedFiles.Length); // "Expected one resolved assembly."
                Assert.Contains("System.Xml", t.ResolvedFiles[1].ItemSpec); // "Expected System.Xml to resolve."
                Assert.Contains("Microsoft.Build.Engine", t.ResolvedFiles[0].ItemSpec); // "Expected Microsoft.Build.Engine to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// deny list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientName()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);

                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);
                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// deny list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientNameWithSubsetTables()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new ITaskItem[] { new TaskItem(@"C:\LocationOfSubset.xml") };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);

                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// This test will verify when the full client name is passed in and it appears in the TargetFrameworkSubsetList, that the
        /// deny list is not used.
        /// </summary>
        [Fact]
        public void ResolveAssemblyReferenceVerifyFullClientNameNoTablesPassedIn()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.BuildEngine = new MockEngine(_output);
                // These are the assemblies we are going to try and resolve
                t.Assemblies = new ITaskItem[] { new TaskItem("System.Xml") };

                // This is a TargetFrameworkSubset that would be searched by RAR if IgnoreDefaultINstalledAssemblySubsetTables does not work.
                t.TargetFrameworkSubsets = new string[] { "Client", "Full" };
                t.FullTargetFrameworkSubsetNames = new string[] { "Full" };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                t.IgnoreDefaultInstalledAssemblySubsetTables = true;
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                Execute(t);

                MockEngine engine = (MockEngine)t.BuildEngine;
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.NoExclusionListBecauseofFullClientName", "Full"));
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Verify the correct references are still in the references table and that references which are in the deny list are not in the references table
        /// Also verify any expected warning messages are seen in the log.
        /// </summary>
        private static void VerifyReferenceTable(ReferenceTable referenceTable, MockEngine mockEngine, AssemblyNameExtension engineAssemblyName, AssemblyNameExtension dataAssemblyName, AssemblyNameExtension sqlclientAssemblyName, AssemblyNameExtension xmlAssemblyName, string warningMessage, string warningMessage2)
        {
            IDictionary<AssemblyNameExtension, Reference> table = referenceTable.References;
            Assert.Equal(3, table.Count); // "Expected there to be three elements in the dictionary"
            Assert.False(table.ContainsKey(sqlclientAssemblyName)); // "Expected to not find the sqlclientAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(xmlAssemblyName)); // "Expected to find the xmlssemblyName in the referenceList"
            Assert.True(table.ContainsKey(dataAssemblyName)); // "Expected to find the dataAssemblyName in the referenceList"
            Assert.True(table.ContainsKey(engineAssemblyName)); // "Expected to find the engineAssemblyName in the referenceList"
            if (warningMessage != null)
            {
                mockEngine.AssertLogContains(warningMessage);
            }
            if (warningMessage2 != null)
            {
                mockEngine.AssertLogContains(warningMessage2);
            }
            table.Clear();
        }

        /// <summary>
        /// Generate helper delegates for returning the file existence and the assembly name.
        /// Also run the rest and return the result.
        /// </summary>
        private bool GenerateHelperDelegatesAndExecuteTask(ResolveAssemblyReference t)
        {
            FileExists cachedFileExists = fileExists;
            GetAssemblyName cachedGetAssemblyName = getAssemblyName;
            string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine.dll");
            string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");
            fileExists = new FileExists(delegate (string path)
{
    if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }
    return false;
});

            getAssemblyName = new GetAssemblyName(delegate (string path)
            {
                if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }
                else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                }

                return null;
            });

            bool success;
            try
            {
                success = Execute(t);
            }
            finally
            {
                fileExists = cachedFileExists;
                getAssemblyName = cachedGetAssemblyName;
            }
            return success;
        }

        [Fact]
        public void DoNotAssumeFilesDescribedByRedistListExistOnDisk()
        {
            string redistListPath = CreateGenericRedistList();
            try
            {
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = new MockEngine(_output);

                t.Assemblies = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build.Engine"),
                new TaskItem("System.Xml")
            };

                t.SearchPaths = new string[]
            {
                @"{TargetFrameworkDirectory}"
            };
                t.TargetFrameworkDirectories = new string[] { Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5") };
                string microsoftBuildEnginePath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\Microsoft.Build.Engine");
                string systemXmlPath = Path.Combine(ObjectModelHelpers.TempProjectDir, "v3.5\\System.Xml.dll");

                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(redistListPath) };

                FileExists cachedFileExists = fileExists;
                GetAssemblyName cachedGetAssemblyName = getAssemblyName;

                // Note that Microsoft.Build.Engine.dll does not exist
                fileExists = new FileExists(delegate (string path)
                {
                    if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase) || path.EndsWith("RarCache", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    return false;
                });

                getAssemblyName = new GetAssemblyName(delegate (string path)
                {
                    if (String.Equals(path, microsoftBuildEnginePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("Microsoft.Build.Engine, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }
                    else if (String.Equals(path, systemXmlPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return new AssemblyNameExtension("System.Xml, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    }

                    return null;
                });

                bool success;
                try
                {
                    success = Execute(t);
                }
                finally
                {
                    fileExists = cachedFileExists;
                    getAssemblyName = cachedGetAssemblyName;
                }

                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
                Assert.Contains("System.Xml", t.ResolvedFiles[0].ItemSpec); // "Expected System.Xml to resolve."
            }
            finally
            {
                File.Delete(redistListPath);
            }
        }

        /// <summary>
        /// Here's how you get into this situation:
        ///
        /// App
        ///   References - A
        ///
        ///    And, the following conditions.
        ///     $(ReferencePath) = c:\apath;:
        ///
        /// Expected result:
        /// * Invalid paths should be ignored.
        ///
        /// </summary>
        [Fact]
        public void Regress397129_HandleInvalidDirectoriesAndFiles_Case1()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"c:\apath",
                @":"
            };

            Execute(t); // Expect no exception.
        }

        /// <summary>
        /// Here's how you get into this situation:
        ///
        /// App
        ///   References - A
        ///        Hintpath=||invalidpath||
        ///
        /// Expected result:
        /// * No exceptions.
        ///
        /// </summary>
        [Fact]
        public void Regress397129_HandleInvalidDirectoriesAndFiles_Case2()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.Assemblies[0].SetMetadata("HintPath", @"||invalidpath||");

            t.SearchPaths = new string[]
            {
                @"{HintPathFromItem}"
            };

            Execute(t);
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        /// <code>
        /// <![CDATA[
        /// References - A
        ///      Depends on B
        ///      Will be found by hintpath.
        /// References -B
        ///      No hintpath
        ///      Exists in A.dll's folder.
        /// ]]>
        /// </code>
        /// B.dll should be unresolved even though its in A's folder because primary resolution needs to work
        /// without looking at dependencies because of the load-time perf scenarios don't look at dependencies.
        /// We must be consistent between primaries resolved with FindDependencies=true and FindDependencies=false.
        /// </summary>
        [Fact]
        public void ByDesignRelatedTo454863_PrimaryReferencesDontResolveToParentFolders()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B")
            };
            t.Assemblies[0].SetMetadata("HintPath", s_regress454863_ADllPath);

            t.SearchPaths = new string[]
            {
                "{HintPathFromItem}"
            };

            Execute(t);

            Assert.True(ContainsItem(t.ResolvedFiles, s_regress454863_ADllPath), "Expected A.dll to be resolved.");
            Assert.True(!ContainsItem(t.ResolvedFiles, s_regress454863_BDllPath), "Expected B.dll to be *not* be resolved.");
        }

        [Fact]
        public void Regress393931_AllowAlternateAssemblyExtensions_Case1()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"C:\Regress393931"
            };
            t.AllowedAssemblyExtensions = new string[]
            {
                ".metaData_dll"
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\Regress393931\A.metadata_dll")); // "Expected A.dll to be resolved."
        }

        /// <summary>
        /// Allow alternate extension values to be passed in.
        /// </summary>
        [Fact]
        public void Regress393931_AllowAlternateAssemblyExtensions()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A")
            };

            t.SearchPaths = new string[]
            {
                @"C:\Regress393931"
            };
            t.AllowedAssemblyExtensions = new string[]
            {
                ".metaData_dll"
            };

            Execute(t);

            // Expect a suggested redirect plus a warning
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\Regress393931\A.metadata_dll")); // "Expected A.dll to be resolved."
        }

        [Fact]
        public void SGenDependeicies()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.Assemblies = new TaskItem[]
            {
                new TaskItem("mycomponent"),
                new TaskItem("mycomponent2")
            };

            t.AssemblyFiles = new TaskItem[]
            {
                new TaskItem(@"c:\SGenDependeicies\mycomponent.dll"),
                new TaskItem(@"c:\SGenDependeicies\mycomponent2.dll")
            };

            t.SearchPaths = new string[]
            {
                @"c:\SGenDependeicies"
            };

            t.FindSerializationAssemblies = true;

            Execute(t);

            Assert.True(t.FindSerializationAssemblies); // "Expected to find serialization assembly."
            Assert.True(ContainsItem(t.SerializationAssemblyFiles, @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll")); // "Expected to find serialization assembly, but didn't."
            Assert.True(ContainsItem(t.SerializationAssemblyFiles, @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll")); // "Expected to find serialization assembly, but didn't."
        }

        /// <summary>
        /// Consider this dependency chain:
        ///
        /// App
        ///   Has project reference to c:\Regress315619\A\MyAssembly.dll
        ///   Has project reference to c:\Regress315619\B\MyAssembly.dll
        ///
        /// These two project references have different versions. Important: PKT is null.
        /// </summary>
        [Fact]
        public void Regress315619_TwoWeaklyNamedPrimariesIsInsoluble()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            MockEngine e = new MockEngine(_output);
            t.BuildEngine = e;

            t.AssemblyFiles = new ITaskItem[]
            {
                new TaskItem(@"c:\Regress315619\A\MyAssembly.dll"),
                new TaskItem(@"c:\Regress315619\B\MyAssembly.dll")
            };

            t.SearchPaths = new string[]
            {
                @"c:\Regress315619\A",
                @"c:\Regress315619\B"
            };

            Execute(t);

            e.AssertLogContains(
                String.Format(AssemblyResources.GetString("ResolveAssemblyReference.ConflictUnsolvable"), @"MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", "MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null"));
        }

        /// <summary>
        /// This is a fix to help ClickOnce folks correctly display information about which
        /// redist components can be deployed.
        ///
        /// Two new attributes are added to resolved references:
        /// (1) IsRedistRoot (bool) -- The flag from the redist *.xml file. If there is no
        /// flag in the file then there will be no flag on the resulting item. This flag means
        /// "I am the UI representative for this entire redist". ClickOnce will use this to hide
        /// all other redist items and to show only this item.
        ///
        /// (2) Redist (string) -- This the value of FileList Redist from the *.xml file.
        /// This string means "I am the unique name of this entire redist".
        ///
        /// </summary>
        [Fact]
        public void ForwardRedistRoot()
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = new MockEngine(_output);

            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("MyRedistRootAssembly"),
                new TaskItem("MyOtherAssembly"),
                new TaskItem("MyThirdAssembly")
            };

            t.SearchPaths = new string[]
            {
                @"c:\MyRedist"
            };

            string redistFile = FileUtilities.GetTemporaryFileName();

            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                        "<File IsRedistRoot='true' AssemblyName='MyRedistRootAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                        "<File IsRedistRoot='false' AssemblyName='MyOtherAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                        "<File AssemblyName='MyThirdAssembly' Version='0.0.0.0' PublicKeyToken='null' Culture='Neutral' FileVersion='2.0.40824.0' InGAC='true'/>" +
                    "</FileList >");

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistFile) };

                Execute(t);
            }
            finally
            {
                File.Delete(redistFile);
            }

            Assert.Equal(3, t.ResolvedFiles.Length); // "Expected three assemblies to be found."
            Assert.Equal("true", t.ResolvedFiles[1].GetMetadata("IsRedistRoot"));
            Assert.Equal("false", t.ResolvedFiles[0].GetMetadata("IsRedistRoot"));
            Assert.Equal("", t.ResolvedFiles[2].GetMetadata("IsRedistRoot"));

            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[0].GetMetadata("Redist"));
            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[1].GetMetadata("Redist"));
            Assert.Equal("Microsoft-Windows-CLRCoreComp", t.ResolvedFiles[2].GetMetadata("Redist"));
        }

        /// <summary>
        /// helper for  TargetFrameworkFiltering
        /// </summary>
        private int RunTargetFrameworkFilteringTest(string projectTargetFramework)
        {
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = new MockEngine(_output);
            t.Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B"),
                new TaskItem("C")
            };

            t.SearchPaths = new string[]
            {
                s_myLibrariesRootPath
            };

            t.Assemblies[1].SetMetadata("RequiredTargetFramework", "3.0");
            t.Assemblies[2].SetMetadata("RequiredTargetFramework", "3.5");
            t.TargetFrameworkVersion = projectTargetFramework;

            Execute(t);

            int set = 0;
            foreach (ITaskItem item in t.ResolvedFiles)
            {
                int mask = 0;
                if (item.ItemSpec.EndsWith(@"\A.dll"))
                {
                    mask = 1;
                }
                else if (item.ItemSpec.EndsWith(@"\B.dll"))
                {
                    mask = 2;
                }
                else if (item.ItemSpec.EndsWith(@"\C.dll"))
                {
                    mask = 4;
                }
                Assert.NotEqual(0, mask); // "Unexpected assembly in resolved list."
                Assert.Equal(0, mask & set); // "Assembly found twice in resolved list."
                set |= mask;
            }
            return set;
        }

        /// <summary>
        /// Make sure the reverse assembly name comparer correctly sorts the assembly names in reverse order
        /// </summary>
        [Fact]
        public void ReverseAssemblyNameExtensionComparer()
        {
            IComparer sortByVersionDescending = new RedistList.SortByVersionDescending();
            AssemblyEntry a1 = new AssemblyEntry("Microsoft.Build.Engine", "1.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a2 = new AssemblyEntry("Microsoft.Build.Engine", "2.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", false);
            AssemblyEntry a3 = new AssemblyEntry("Microsoft.Build.Engine", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a4 = new AssemblyEntry("A", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);
            AssemblyEntry a5 = new AssemblyEntry("B", "3.0.0.0", "b03f5f7f11d50a3a", "neutral", true, true, "Foo", "none", true);

            // Verify versions sort correctly when simple name is same
            Assert.Equal(0, sortByVersionDescending.Compare(a1, a1));
            Assert.Equal(1, sortByVersionDescending.Compare(a1, a2));
            Assert.Equal(1, sortByVersionDescending.Compare(a1, a3));
            Assert.Equal(-1, sortByVersionDescending.Compare(a2, a1));
            Assert.Equal(1, sortByVersionDescending.Compare(a2, a3));

            // Verify the names sort alphabetically
            Assert.Equal(-1, sortByVersionDescending.Compare(a4, a5));
        }

        /// <summary>
        /// Check the Filtering based on Target Framework.
        /// </summary>
        [Fact]
        public void TargetFrameworkFiltering()
        {
            int resultSet = RunTargetFrameworkFilteringTest("3.0");
            Assert.Equal(0x3, resultSet); // "Expected assemblies A & B to be found."

            resultSet = RunTargetFrameworkFilteringTest("3.5");
            Assert.Equal(0x7, resultSet); // "Expected assemblies A, B & C to be found."

            resultSet = RunTargetFrameworkFilteringTest(null);
            Assert.Equal(0x7, resultSet); // "Expected assemblies A, B & C to be found."

            resultSet = RunTargetFrameworkFilteringTest("2.0");
            Assert.Equal(0x1, resultSet); // "Expected only assembly A to be found."
        }

        /// <summary>
        /// Verify the when a simple name is asked for that the assemblies are returned in sorted order by version.
        /// </summary>
        [Fact]
        public void VerifyGetSimpleNamesIsSorted()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='3.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='100.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                        "<File AssemblyName='System' Version='2.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                List<AssemblyEntry> entryArray = redist.FindAssemblyNameFromSimpleName("System").ToList();
                Assert.Equal(6, entryArray.Count);
                AssemblyNameExtension a1 = new AssemblyNameExtension(entryArray[0].FullName);
                AssemblyNameExtension a2 = new AssemblyNameExtension(entryArray[1].FullName);
                AssemblyNameExtension a3 = new AssemblyNameExtension(entryArray[2].FullName);
                AssemblyNameExtension a4 = new AssemblyNameExtension(entryArray[3].FullName);
                AssemblyNameExtension a5 = new AssemblyNameExtension(entryArray[4].FullName);
                AssemblyNameExtension a6 = new AssemblyNameExtension(entryArray[5].FullName);

                Assert.Equal(new Version("100.0.0.0"), a1.Version);
                Assert.Equal(new Version("10.0.0.0"), a2.Version);
                Assert.Equal(new Version("4.0.0.0"), a3.Version);
                Assert.Equal(new Version("3.0.0.0"), a4.Version);
                Assert.Equal(new Version("2.0.0.0"), a5.Version);
                Assert.Equal(new Version("1.0.0.0"), a6.Version);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does not have the correct redist name , Microsoft-Windows-CLRCoreComp then we should not consider it a framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListNonWindowsRedistName()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does have the correct redist name , Microsoft-Windows-CLRCoreComp then we should consider it a framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListWindowsRedistName()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Something' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// If the assembly was found in a redis list which does have the correct redist name , Microsoft-Windows-CLRCoreComp then we should consider it a framework assembly taking into account including partial matching
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListPartialMatches()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System, PublicKeyToken=b77a5c561934e089");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);

                a1 = new AssemblyNameExtension("System");
                inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }
        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The version should not be compared
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffVersion()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=5.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.True(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The public key is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffPublicKey()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=5.0.0.0, Culture=Neutral, PublicKeyToken=b67a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The Culture is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffCulture()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='FR-fr' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("System, Version=10.0.0.0, Culture=Neutral, PublicKeyToken=b67a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when we ask if an assembly is in the redist list we get the right answer.
        /// The SimpleName is significant and should make the match not work
        /// </summary>
        [Fact]
        public void VerifyAssemblyInRedistListDiffSimpleName()
        {
            string redistFile = FileUtilities.GetTemporaryFileName();
            try
            {
                File.WriteAllText(
                    redistFile,
                    "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                        "<File AssemblyName='System' Version='10.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                    "</FileList >");

                AssemblyTableInfo tableInfo = new AssemblyTableInfo(redistFile, "DoesNotExist");
                RedistList redist = RedistList.GetRedistList(new AssemblyTableInfo[] { tableInfo });

                AssemblyNameExtension a1 = new AssemblyNameExtension("Something, Version=10.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089");
                bool inRedistList = redist.FrameworkAssemblyEntryInRedist(a1);
                Assert.False(inRedistList);
            }
            finally
            {
                File.Delete(redistFile);
            }
        }

        /// <summary>
        /// Verify when a p2p (assemblies in the AssemblyFiles property) are passed to rar that we properly un-resolve them if they depend on references which are in the deny list for the profile.
        /// </summary>
        [Fact]
        public void Verifyp2pAndProfile()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "Verifyp2pAndProfile");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");

            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                t.AssemblyFiles = new ITaskItem[] { new TaskItem(Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll")) };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";
                t.TargetFrameworkMoniker = ".Net Framework, Version=v4.0";

                bool success = Execute(t, false);
                Assert.True(success); // "Expected no errors."
                Assert.Empty(t.ResolvedFiles); // "Expected no resolved assemblies."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"), "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.TargetFrameworkMoniker);
                e.AssertLogContains(warningMessage);
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a p2p (assemblies in the AssemblyFiles property) are passed to rar that we properly resolve them if they depend on references which are in the deny list for the profile but have specific version set to true.
        /// </summary>
        [Fact]
        public void Verifyp2pAndProfile2()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "Verifyp2pAndProfile");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");

            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                TaskItem item = new TaskItem(Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"));
                item.SetMetadata("SpecificVersion", "true");
                t.AssemblyFiles = new ITaskItem[] { item };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";

                bool success = Execute(t);
                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected no resolved assemblies."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"), "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "Client");
                e.AssertLogDoesntContain(warningMessage);
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a profile is used that assemblies not in the profile are excluded or have metadata attached to indicate there are dependencies
        /// which are not in the profile.
        /// </summary>
        [Fact]
        public void VerifyClientProfileRedistListAndProfileList()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyClientProfileRedistListAndProfileList");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");
            try
            {
                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(_fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
                Assert.Contains("Microsoft.Build.Engine", t.ResolvedFiles[0].ItemSpec); // "Expected Engine to resolve."
                e.AssertLogContains("MSB3252");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify when a profile is used that assemblies not in the profile are excluded or have metadata attached to indicate there are dependencies
        /// which are not in the profile.
        ///
        /// Make sure the ProfileFullFrameworkAssemblyTable parameter works.
        /// </summary>
        [Fact]
        public void VerifyClientProfileRedistListAndProfileList2()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyClientProfileRedistListAndProfileList2");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");
            try
            {
                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(_fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                ITaskItem item = new TaskItem(fullRedistList);
                item.SetMetadata("FrameworkDirectory", Path.GetDirectoryName(fullRedistList));
                t.FullFrameworkAssemblyTables = new ITaskItem[] { item };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected no errors."
                Assert.Single(t.ResolvedFiles); // "Expected one resolved assembly."
                Assert.Contains("Microsoft.Build.Engine", t.ResolvedFiles[0].ItemSpec); // "Expected Engine to resolve."
                e.AssertLogContains("MSB3252");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// When targeting a profile make sure that we do not resolve the assembly if we reference something from the full framework which is in the GAC.
        /// This will cover the same where we are referencing a full framework assembly.
        /// </summary>
        [Fact]
        public void VerifyAssemblyInGacButNotInProfileIsNotResolved()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyAssemblyInGacButNotInProfileIsNotResolved");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");
            useFrameworkFileExists = true;
            string fullRedistListContents =
            "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='System' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='Neutral'/>" +
            "</FileList >";

            try
            {
                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(fullRedistListContents, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                TaskItem item = new TaskItem(@"DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
                t.Assemblies = new ITaskItem[] { item };
                t.SearchPaths = new string[] { s_myComponents40ComponentPath, "{GAC}" };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;
                t.FullFrameworkFolders = new string[] { fullFrameworkDirectory };
                t.LatestTargetFrameworkDirectories = new string[] { fullFrameworkDirectory };
                t.ProfileName = "Client";
                t.TargetFrameworkMoniker = ".NETFramework, Version=4.0";

                bool success = Execute(t, false);
                Console.Out.WriteLine(e.Log);
                Assert.True(success); // "Expected no errors."
                Assert.Empty(t.ResolvedFiles); // "Expected no files to resolved."
                string warningMessage = t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", "DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "SysTem, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.TargetFrameworkMoniker);
                e.AssertLogContains(warningMessage);
            }
            finally
            {
                useFrameworkFileExists = false;
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Make sure when reading in the full framework redist list or when reading in the allow list xml files.
        /// Errors in reading the file should be logged as warnings and no assemblies should be excluded.
        ///
        /// </summary>
        [Fact]
        public void VerifyProfileErrorsAreLogged()
        {
            string fullFrameworkDirectory = Path.Combine(Path.GetTempPath(), "VerifyProfileErrorsAreLogged");
            string targetFrameworkDirectory = Path.Combine(fullFrameworkDirectory, "Profiles", "Client");
            try
            {
                string fullRedistListContentsErrors =
                  "<FileList Redist='Microsoft-Windows-CLRCoreComp'>" +
                        "File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' >" +
                        "File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' >" +
                   "";

                // Create a generic redist list with system.xml and microsoft.build.engine.
                string profileRedistList;
                string fullRedistList;
                GenerateRedistAndProfileXmlLocations(fullRedistListContentsErrors, _engineOnlySubset, out profileRedistList, out fullRedistList, fullFrameworkDirectory, targetFrameworkDirectory);

                ResolveAssemblyReference t = new ResolveAssemblyReference();
                MockEngine e = new MockEngine(_output);
                t.BuildEngine = e;
                t.Assemblies = new ITaskItem[] { new TaskItem("Microsoft.Build.Engine"), new TaskItem("System.Xml") };
                t.SearchPaths = new string[] { @"{TargetFrameworkDirectory}", fullFrameworkDirectory };
                t.TargetFrameworkDirectories = new string[] { targetFrameworkDirectory };
                t.InstalledAssemblyTables = new ITaskItem[] { new TaskItem(profileRedistList) };
                t.IgnoreDefaultInstalledAssemblyTables = true;

                ITaskItem item = new TaskItem(fullRedistList);
                item.SetMetadata("FrameworkDirectory", Path.GetDirectoryName(fullRedistList));
                t.FullFrameworkAssemblyTables = new ITaskItem[] { item };
                t.ProfileName = "Client";

                string microsoftBuildEnginePath = Path.Combine(fullFrameworkDirectory, "Microsoft.Build.Engine.dll");
                string systemXmlPath = Path.Combine(targetFrameworkDirectory, "System.Xml.dll");

                bool success = GenerateHelperDelegatesAndExecuteTask(t, microsoftBuildEnginePath, systemXmlPath);
                Assert.True(success); // "Expected errors."
                Assert.Equal(2, t.ResolvedFiles.Length); // "Expected two resolved assembly."
                e.AssertLogContains("MSB3263");
            }
            finally
            {
                if (Directory.Exists(fullFrameworkDirectory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(fullFrameworkDirectory, true);
                }
            }
        }

        /// <summary>
        /// Generate the full framework and profile redist list directories and files
        /// </summary>
        private static void GenerateRedistAndProfileXmlLocations(string fullRedistContents, string profileListContents, out string profileRedistList, out string fullRedistList, string fullFrameworkDirectory, string targetFrameworkDirectory)
        {
            fullRedistList = Path.Combine(fullFrameworkDirectory, "RedistList", "FrameworkList.xml");
            string redistDirectory = Path.GetDirectoryName(fullRedistList);
            if (Directory.Exists(redistDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redistDirectory);
            }

            Directory.CreateDirectory(redistDirectory);

            File.WriteAllText(fullRedistList, fullRedistContents);

            profileRedistList = Path.Combine(targetFrameworkDirectory, "RedistList", "FrameworkList.xml");

            redistDirectory = Path.GetDirectoryName(profileRedistList);
            if (Directory.Exists(redistDirectory))
            {
                FileUtilities.DeleteWithoutTrailingBackslash(redistDirectory);
            }

            Directory.CreateDirectory(redistDirectory);

            File.WriteAllText(profileRedistList, profileListContents);
        }

        [Fact]
        public void SDKReferencesAreResolvedWithoutIO()
        {
            InitializeRARwithMockEngine(_output, out MockEngine mockEngine, out ResolveAssemblyReference rar);

            string refPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            TaskItem item = new TaskItem(refPath);
            item.SetMetadata("ExternallyResolved", "true");

            item.SetMetadata("FrameworkReferenceName", "Microsoft.NETCore.App");
            item.SetMetadata("FrameworkReferenceVersion", "8.0.0");

            item.SetMetadata("AssemblyName", "System.Candy");
            item.SetMetadata("AssemblyVersion", "8.1.2.3");
            item.SetMetadata("PublicKeyToken", "b03f5f7f11d50a3a");

            rar.Assemblies = new ITaskItem[] { item };
            rar.SearchPaths = new string[]
            {
                "{CandidateAssemblyFiles}",
                "{HintPathFromItem}",
                "{TargetFrameworkDirectory}",
                "{RawFileName}",
            };
            rar.WarnOrErrorOnTargetArchitectureMismatch = "Warning";

            // Execute RAR and assert that we receive no I/O callbacks because the task gets what it needs from item metadata.
            rar.Execute(
                _ => throw new ShouldAssertException("Unexpected FileExists callback"),
                directoryExists,
                getDirectories,
                _ => throw new ShouldAssertException("Unexpected GetAssemblyName callback"),
                (string path, ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache, out AssemblyNameExtension[] dependencies, out string[] scatterFiles, out FrameworkNameVersioning frameworkName)
                  => throw new ShouldAssertException("Unexpected GetAssemblyMetadata callback"),
#if FEATURE_WIN32_REGISTRY
                getRegistrySubKeyNames,
                getRegistrySubKeyDefaultValue,
#endif
                _ => throw new ShouldAssertException("Unexpected GetLastWriteTime callback"),
                _ => throw new ShouldAssertException("Unexpected GetAssemblyRuntimeVersion callback"),
#if FEATURE_WIN32_REGISTRY
                openBaseKey,
#endif
                checkIfAssemblyIsInGac,
                isWinMDFile,
                readMachineTypeFromPEHeader).ShouldBeTrue();

            rar.ResolvedFiles.Length.ShouldBe(1);
            rar.ResolvedFiles[0].ItemSpec.ShouldBe(refPath);
            rar.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("System.Candy, Version=8.1.2.3, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

            // The reference is not worth persisting in the per-instance cache.
            rar._cache.IsDirty.ShouldBeFalse();
        }

        [Fact]
        public void ManagedRuntimeVersionReaderSupportsWindowsRuntime()
        {
            // This is a prefix of a .winmd file built using the Universal Windows runtime component project in Visual Studio.
            string windowsRuntimeAssemblyHeaderBase64Encoded =
                "TVqQAAMAAAAEAAAA//8AALgAAAAAAAAAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAA4fug4AtAnNIbgBTM0hVGhpcyBwcm9ncmFtIGNhbm5vdCBiZSBydW4gaW4gRE9TIG1v" +
                "ZGUuDQ0KJAAAAAAAAABQRQAATAEDAFD4XWQAAAAAAAAAAOAAIiALATAAAAwAAAAGAAAAAAAAXioAAAAgAAAAQAAAAAAAEAAgAAAAAgAABAAAAAAAAAAGAAIAAAAAAACAAAAAAgAAAAAAAAMAYIUAABAA" +
                "ABAAAAAAEAAAEAAAAAAAABAAAAAAAAAAAAAAAAkqAABPAAAAAEAAANADAAAAAAAAAAAAAAAAAAAAAAAAAGAAAAwAAABwKQAAHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
                "AAAAIAAACAAAAAAAAAAAAAAACCAAAEgAAAAAAAAAAAAAAC50ZXh0AAAAZAoAAAAgAAAADAAAAAIAAAAAAAAAAAAAAAAAACAAAGAucnNyYwAAANADAAAAQAAAAAQAAAAOAAAAAAAAAAAAAAAAAABAAABA" +
                "LnJlbG9jAAAMAAAAAGAAAAACAAAAEgAAAAAAAAAAAAAAAAAAQAAAQgAAAAAAAAAAAAAAAAAAAAA9KgAAAAAAAEgAAAACAAUAWCAAABgJAAABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAB4CKAEAAAoqQlNKQgEAAQAAAAAAJAAAAFdpbmRvd3NSdW50aW1lIDEuNDtDTFIgdjQuMC4zMDMxOQAAAAAABQCEAAAA+AIAACN+AAB8AwAAoAMAACNTdHJpbmdz" +
                "AAAAABwHAAAIAAAAI1VTACQHAAAQAAAAI0dVSUQAAAA0BwAA5AEAACNCbG9iAAAAAAAAAAIAAAFHFwACCQAAAAD6ATMAFgAAAQAAABwAAAAEAAAAAwAAAAEAAAADAAAAFwAAABwAAAABAAAAAQAAAAMA" +
                "AAAAAE0AAQAAAAAABgCWA9ACCgCWA9ACDgBlANcCBgDdATQDBgBbAjQDBgC4AAIDGwBUAwAABgD1AOoCBgCPAeoCBgBwAeoCBgBCAuoCBgD9AeoCBgAWAuoCBgAfAeoCBgBTAeoCBgDhABUDBgA6AX8C" +
                "DgDBASgADgCAACgADgAMASgADgDBAigADgBfASgADgDMACgADgAxAigABgCPADQDDgCqACgADgCsASgACgCKANACAAAAAB8AAAAAAAEAAQAABRAAAQANAAUAAQABAAFBEAAGAA0ACQABAAIAoEAAAGMD" +
                "DQAAAAEABABQIAAAAACGGPwCAQABAAAAAAADAIYY/AIBAAEAAAAAAAMA4QGZAgUAAQAAAAAAeQICABAAAwAQAAMADQAJAPwCAQAZALgCBQAhAPwCCQApAPwCAQAxAPwCDgBBAPwCFABJAPwCFABRAPwC" +
                "FABZAPwCFABhAPwCFABpAPwCFABxAPwCFAB5APwCFACBAPwCGQCJAPwCFACRAPwCHgChAPwCJACxAPwCKgC5APwCKgDBAPwCAQDJAPwCAQDRAPwCLwDZAPwCPgAlAKMAqgEuABsA2AAuACMA4QAuACsA" +
                "AAEuADMACQEuADsAIAEuAEMAIAEuAEsAIAEuAFMACQEuAFsAJgEuAGMAIAEuAGsAPgEuAHMAIAEuAHsASwFDAIMAAAFDAIsAmAFDAJMAoQFDAJsAoQFFAKMAqgFjAIMAAAFjAIsAmAFjAJMAoQFjAKsA" +
                "qgFjAJsAoQGDAKsAqgGDALMArwGDAJMAoQGDALsAxAEDAAYABQAEgAAAAQAAAAAAAAAAAgAAAAANAAAABAACAAEAAAAAAAAARABxAAAAAAD/AP8A/wD/AAAAAABNAEQAAABWAAQAAAAAAAAAAAIAAAAA";

            using MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(windowsRuntimeAssemblyHeaderBase64Encoded));
            using BinaryReader reader = new BinaryReader(memoryStream);
            string runtimeVersion = ManagedRuntimeVersionReader.GetRuntimeVersion(reader);

            runtimeVersion.ShouldBe("WindowsRuntime 1.4;CLR v4.0.30319");
        }
    }
}
