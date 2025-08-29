// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit tests for the ResolveAssemblyReference task.
    /// </summary>
    public sealed class WinMDTests : ResolveAssemblyReferenceTestFixture
    {
        public WinMDTests(ITestOutputHelper output) : base(output)
        {
        }

        #region AssemblyInformationIsWinMDFile Tests

        /// <summary>
        /// Verify a null file path passed in return the fact the file is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileNullFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(null, getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// Verify if a empty file path is passed in that the file is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileEmptyFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(String.Empty, getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// If the file does not exist then we should report this is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileFileDoesNotExistFilePath()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleDoesNotExist.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// The file exists and has the correct windowsruntime metadata, we should report this is a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileGoodFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.True(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// This file is a mixed file with CLR and windowsruntime metadata we should report this is a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileMixedFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.True(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.True(isManagedWinMD);
        }

        /// <summary>
        /// The file has only CLR metadata we should report this is not a winmd file
        /// </summary>
        [Fact]
        public void IsWinMDFileCLROnlyFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleClrOnly.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// The windows runtime string is not correctly formatted, report this is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileBadWindowsRuntimeFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\WinMD\SampleBadWindowsRuntime.Winmd", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// We should report that a regular net assembly is not a winmd file.
        /// </summary>
        [Fact]
        public void IsWinMDFileRegularNetAssemblyFile()
        {
            string imageRuntime;
            bool isManagedWinMD;
            Assert.False(AssemblyInformation.IsWinMDFile(@"C:\Framework\Whidbey\System.dll", getRuntimeVersion, fileExists, out imageRuntime, out isManagedWinMD));
            Assert.False(isManagedWinMD);
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that
        /// the winmd references get the correct metadata applied to them
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VerifyP2PHaveCorrectMetadataWinMD(bool setImplementationMetadata)
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem taskItem = new TaskItem(@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd");

            if (setImplementationMetadata)
            {
                taskItem.SetMetadata(ItemMetadataNames.winmdImplmentationFile, "SampleWindowsRuntimeOnly.dll");
            }

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                taskItem
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(2, t.RelatedFiles.Length);

            bool dllFound = false;
            bool priFound = false;

            foreach (ITaskItem item in t.RelatedFiles)
            {
                if (item.ItemSpec.EndsWith(@"C:\WinMD\SampleWindowsRuntimeOnly.dll"))
                {
                    dllFound = true;
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.imageRuntime));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winMDFile));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                }
                if (item.ItemSpec.EndsWith(@"C:\WinMD\SampleWindowsRuntimeOnly.pri"))
                {
                    priFound = true;

                    Assert.Empty(item.GetMetadata(ItemMetadataNames.imageRuntime));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winMDFile));
                    Assert.Empty(item.GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                }
            }

            Assert.True(dllFound && priFound); // "Expected to find .dll and .pri related files."
            Assert.Empty(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.Equal("Native", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFileType));
            Assert.Equal("SampleWindowsRuntimeOnly.dll", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
            Assert.Equal("WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
        }

        [Fact]
        public void VerifyP2PHaveCorrectMetadataWinMDStaticLib()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem taskItem = new TaskItem(@"C:\WinMDLib\LibWithWinmdAndNoDll.Winmd");

            taskItem.SetMetadata(ItemMetadataNames.winmdImplmentationFile, "LibWithWinmdAndNoDll.lib");

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                taskItem
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };

            Execute(t).ShouldBeTrue();

            engine.Errors.ShouldBe(0);
            engine.Warnings.ShouldBe(0);

            t.ResolvedFiles.ShouldHaveSingleItem();
            t.RelatedFiles.ShouldHaveSingleItem();

            t.RelatedFiles[0].ItemSpec.ShouldBe(@"C:\WinMDLib\LibWithWinmdAndNoDll.pri",
                "Expected to find .pri related files but NOT the lib.");

            t.RelatedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime).ShouldBeEmpty();
            t.RelatedFiles[0].GetMetadata(ItemMetadataNames.winMDFile).ShouldBeEmpty();
            t.RelatedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).ShouldBeEmpty();

            t.ResolvedDependencyFiles.ShouldBeEmpty();
            bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)).ShouldBeTrue();
            t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFileType).ShouldBe("Native");
            t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile).ShouldBe("LibWithWinmdAndNoDll.lib");
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that
        /// the winmd references get the correct metadata applied to them
        /// </summary>
        [Fact]
        public void VerifyP2PHaveCorrectMetadataWinMDManaged()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem taskItem = new TaskItem(@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd");

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                taskItem
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Empty(t.RelatedFiles);


            Assert.Empty(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.Equal("Managed", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFileType));
            Assert.Empty(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
            Assert.Equal("WindowsRuntime 1.0, CLR V2.0.50727", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
        }

        /// <summary>
        /// When a project to project reference is passed in we want to verify that
        /// the winmd references get the correct metadata applied to them
        /// </summary>
        [Fact]
        public void VerifyP2PHaveCorrectMetadataNonWinMD()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"C:\AssemblyFolder\SomeAssembly.dll")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);

            Assert.Empty(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Empty(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile));
            Assert.Equal("v2.0.50727", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
        }

        /// <summary>
        /// Verify when we reference a winmd file as a reference item make sure we ignore the mscorlib.
        /// </summary>
        [Fact]
        public void IgnoreReferenceToMscorlib()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"SampleWindowsRuntimeOnly"), new TaskItem(@"SampleWindowsRuntimeAndClr")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Empty(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            engine.AssertLogDoesntContain("conflict");
        }

        /// <summary>
        /// Verify when we reference a mixed winmd file that we do resolve the reference to the mscorlib
        /// </summary>
        [Fact]
        public void MixedWinMDGoodReferenceToMscorlib()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"SampleWindowsRuntimeAndClr")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Empty(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            engine.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.Resolved", @"C:\WinMD\v4\mscorlib.dll");
        }


        /// <summary>
        /// Verify when a winmd file depends on another winmd file that we do resolve the dependency
        /// </summary>
        [Fact]
        public void WinMdFileDependsOnAnotherWinMDFile()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"SampleWindowsRuntimeOnly2")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.TargetProcessorArchitecture = "X86";
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(@"C:\WinMD\SampleWindowsRuntimeOnly2.winmd", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.Equal(@"C:\WinMD\SampleWindowsRuntimeOnly.winmd", t.ResolvedDependencyFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }



        /// <summary>
        /// We have two dlls which depend on a winmd, the first dll does not have the winmd beside it, the second one does
        /// we want to make sure that the winmd file is resolved beside the second dll.
        /// </summary>
        [Fact]
        public void ResolveWinmdBesideDll()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"C:\DirectoryContainsOnlyDll\A.dll"),
                new TaskItem(@"C:\DirectoryContainsdllAndWinmd\B.dll"),
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { "{RAWFILENAME}" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(@"C:\DirectoryContainsdllAndWinmd\C.winmd", t.ResolvedDependencyFiles[0].ItemSpec);
        }

        /// <summary>
        /// We have a winmd file and a dll depend on a winmd, there are copies of the winmd beside each of the files.
        /// we want to make sure that the winmd file is resolved beside the winmd since that is the first file resolved.
        /// </summary>
        [Fact]
        public void ResolveWinmdBesideDll2()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"C:\DirectoryContainstwoWinmd\A.winmd"),
                new TaskItem(@"C:\DirectoryContainsdllAndWinmd\B.dll"),
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"{RAWFILENAME}" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            Assert.Equal(@"C:\DirectoryContainstwoWinmd\C.winmd", t.ResolvedDependencyFiles[0].ItemSpec);
        }

        /// <summary>
        /// Verify when a winmd file depends on another winmd file that itself has framework dependencies that we do not resolve any of the
        /// dependencies due to the winmd to winmd reference
        /// </summary>
        [Fact]
        public void WinMdFileDependsOnAnotherWinMDFileWithFrameworkDependencies()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"SampleWindowsRuntimeOnly3")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"{TargetFrameworkDirectory}", @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            t.TargetFrameworkDirectories = new string[] { @"c:\WINNT\Microsoft.NET\Framework\v4.0.MyVersion" };
            t.TargetProcessorArchitecture = "x86";
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(4, t.ResolvedDependencyFiles.Length);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.Equal(@"C:\WinMD\SampleWindowsRuntimeOnly3.winmd", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }

        /// <summary>
        /// Make sure when a dot net assembly depends on a WinMDFile that
        /// we get the winmd file resolved. Also make sure that if there is Implementation, ImageRuntime, or IsWinMD set on the dll that
        /// it does not get propagated to the winmd file dependency.
        /// </summary>
        [Fact]
        public void DotNetAssemblyDependsOnAWinMDFile()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem item = new TaskItem(@"DotNetAssemblyDependsOnWinMD");
            // This should not be used for anything, it is recalculated in rar, this is to make sure it is not forwarded to child items.
            item.SetMetadata(ItemMetadataNames.imageRuntime, "FOO");
            // This should not be used for anything, it is recalculated in rar, this is to make sure it is not forwarded to child items.
            item.SetMetadata(ItemMetadataNames.winMDFile, "NOPE");
            item.SetMetadata(ItemMetadataNames.winmdImplmentationFile, "IMPL");
            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                item
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.TargetProcessorArchitecture = "X86";
            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.Equal(@"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"v2.0.50727", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.Equal("NOPE", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile));
            Assert.Equal("IMPL", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));

            Assert.Equal(@"C:\WinMD\SampleWindowsRuntimeOnly.winmd", t.ResolvedDependencyFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
            Assert.Equal("SampleWindowsRuntimeOnly.dll", t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
        }

        /// <summary>
        /// Resolve a winmd file which depends on a native implementation dll that has an invalid pe header.
        /// This will always result in an error since the dll is malformed
        /// </summary>
        [Fact]
        public void ResolveWinmdWithInvalidPENativeDependency()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem item = new TaskItem(@"DependsOnInvalidPeHeader");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            bool succeeded = Execute(t);

            // Should fail since PE Header is not valid and this is always an error.
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            // The original winmd will resolve but its implementation dll must not be there
            Assert.Single(t.ResolvedFiles);
            Assert.Empty(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));

            string invalidPEMessage = ResourceUtilities.GetResourceString("ResolveAssemblyReference.ImplementationDllHasInvalidPEHeader");
            string fullMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.ProblemReadingImplementationDll", @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll", invalidPEMessage);
            engine.AssertLogContains(fullMessage);
        }

        /// <summary>
        /// Resolve a winmd file which depends a native dll that matches the targeted architecture
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependencyMatchingArchitecturesX86()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem item = new TaskItem("DependsOnX86");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = "X86";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";

            bool succeeded = Execute(t);
            Assert.Single(t.ResolvedFiles);

            Assert.Equal(@"C:\WinMDArchVerification\DependsOnX86.winmd", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.True(succeeded);
            Assert.Equal("DependsOnX86.dll", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Resolve a winmd file which depends a native dll that matches the targeted architecture
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependencyAnyCPUNative()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            // IMAGE_FILE_MACHINE unknown is supposed to work on all machine types
            TaskItem item = new TaskItem("DependsOnAnyCPUUnknown");
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = "X86";
            t.WarnOrErrorOnTargetArchitectureMismatch = "Error";

            bool succeeded = Execute(t);
            Assert.Single(t.ResolvedFiles);

            Assert.Equal(@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.winmd", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            Assert.True(succeeded);
            Assert.Equal("DependsOnAnyCPUUnknown.dll", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Resolve a winmd file which depends on a native implementation dll that has an invalid pe header.
        /// A warning or error is expected in the log depending on the WarnOrErrorOnTargetArchitecture property value.
        /// </summary>
        [Fact]
        public void ResolveWinmdWithArchitectureDependency()
        {
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "Error");
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "Warning");
            VerifyImplementationArchitecture("DependsOnX86", "MSIL", "X86", "None");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "Error");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "Warning");
            VerifyImplementationArchitecture("DependsOnX86", "AMD64", "X86", "None");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "Error");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "Warning");
            VerifyImplementationArchitecture("DependsOnAmd64", "MSIL", "AMD64", "None");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "Error");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "Warning");
            VerifyImplementationArchitecture("DependsOnAmd64", "X86", "AMD64", "None");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "Error");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "Warning");
            VerifyImplementationArchitecture("DependsOnARM", "MSIL", "ARM", "None");
            VerifyImplementationArchitecture("DependsOnARMV7", "MSIL", "ARM", "Error");
            VerifyImplementationArchitecture("DependsOnARMV7", "MSIL", "ARM", "Warning");
            VerifyImplementationArchitecture("DependsOnARMv7", "MSIL", "ARM", "None");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "Error");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "Warning");
            VerifyImplementationArchitecture("DependsOnIA64", "MSIL", "IA64", "None");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "Error");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "Warning");
            VerifyImplementationArchitecture("DependsOnUnknown", "MSIL", "Unknown", "None");
        }

        private void VerifyImplementationArchitecture(string winmdName, string targetProcessorArchitecture, string implementationFileArch, string warnOrErrorOnTargetArchitectureMismatch)
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);
            TaskItem item = new TaskItem(winmdName);
            ITaskItem[] assemblyFiles = new TaskItem[] { item };

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMDArchVerification" };
            t.TargetProcessorArchitecture = targetProcessorArchitecture;
            t.WarnOrErrorOnTargetArchitectureMismatch = warnOrErrorOnTargetArchitectureMismatch;

            bool succeeded = Execute(t);
            Assert.Single(t.ResolvedFiles);

            Assert.Equal(@"C:\WinMDArchVerification\" + winmdName + ".winmd", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));

            string fullMessage;
            if (implementationFileArch.Equals("Unknown"))
            {
                fullMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.UnknownProcessorArchitecture", @"C:\WinMDArchVerification\" + winmdName + ".dll", @"C:\WinMDArchVerification\" + winmdName + ".winmd", Tasks.NativeMethods.IMAGE_FILE_MACHINE_R4000.ToString("X", CultureInfo.InvariantCulture));
            }
            else
            {
                fullMessage = ResourceUtilities.FormatResourceStringStripCodeAndKeyword("ResolveAssemblyReference.MismatchBetweenTargetedAndReferencedArchOfImplementation", targetProcessorArchitecture, implementationFileArch, @"C:\WinMDArchVerification\" + winmdName + ".dll", @"C:\WinMDArchVerification\" + winmdName + ".winmd");
            }

            if (warnOrErrorOnTargetArchitectureMismatch.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                engine.AssertLogDoesntContain(fullMessage);
            }
            else
            {
                engine.AssertLogContains(fullMessage);
            }

            if (warnOrErrorOnTargetArchitectureMismatch.Equals("Warning", StringComparison.OrdinalIgnoreCase))
            {
                // Should fail since PE Header is not valid and this is always an error.
                Assert.True(succeeded);
                Assert.Equal(winmdName + ".dll", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                Assert.Equal(0, engine.Errors);
                Assert.Equal(1, engine.Warnings);
            }
            else if (warnOrErrorOnTargetArchitectureMismatch.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                // Should fail since PE Header is not valid and this is always an error.
                Assert.False(succeeded);
                Assert.Empty(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                Assert.Equal(1, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
            else if (warnOrErrorOnTargetArchitectureMismatch.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(succeeded);
                Assert.Equal(winmdName + ".dll", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winmdImplmentationFile));
                Assert.Equal(0, engine.Errors);
                Assert.Equal(0, engine.Warnings);
            }
        }

        /// <summary>
        /// Verify when a winmd file depends on another winmd file that we resolve both and that the metadata is correct.
        /// </summary>
        [Fact]
        public void DotNetAssemblyDependsOnAWinMDFileWithVersion255()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                new TaskItem(@"DotNetAssemblyDependsOn255WinMD")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyFiles;
            t.SearchPaths = new String[] { @"C:\WinMD", @"C:\WinMD\v4\", @"C:\WinMD\v255\" };
            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);

            Assert.Equal(@"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll", t.ResolvedFiles[0].ItemSpec);
            Assert.Equal(@"v2.0.50727", t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.Empty(t.ResolvedFiles[0].GetMetadata(ItemMetadataNames.winMDFile));

            Assert.Equal(@"C:\WinMD\WinMDWithVersion255.winmd", t.ResolvedDependencyFiles[0].ItemSpec);
            Assert.Equal(@"WindowsRuntime 1.0", t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.imageRuntime));
            Assert.True(bool.Parse(t.ResolvedDependencyFiles[0].GetMetadata(ItemMetadataNames.winMDFile)));
        }
        #endregion
    }
}
