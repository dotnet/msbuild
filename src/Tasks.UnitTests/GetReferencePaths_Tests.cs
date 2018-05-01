// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// <copyright file="GetReferenceAssemblyPaths_Tests.cs" company="Microsoft">
// </copyright>
// <summary>Tests for the task which gets the reference assembly paths for a given target framework version / moniker.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Tasks;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using System.IO;
using Microsoft.Build.Utilities;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using Xunit;

#if !RUNTIME_TYPE_NETCORE
namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the task which gets the reference assembly paths for a given target framework version / moniker
    /// </summary>
    sealed public class GetReferenceAssmeblyPath_Tests
    {
        /// <summary>
        /// Test the case where there is a good target framework moniker passed in.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGood()
        {
            string targetFrameworkMoniker = ".NetFramework, Version=v4.5";
            MockEngine engine = new MockEngine();
            GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
            getReferencePaths.BuildEngine = engine;
            getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
            getReferencePaths.Execute();
            string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
            Assert.Equal(ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkNameVersioning(targetFrameworkMoniker)).Count, returnedPaths.Length);
            Assert.Equal(0, engine.Errors); // "Expected the log to contain no errors"
        }

        /// <summary>
        /// Test the case where there is a good target framework moniker passed in.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGoodWithRoot()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "TestGeneralFrameworkMonikerGoodWithRoot");
            string framework41Directory = Path.Combine(tempDirectory, Path.Combine("MyFramework", "v4.1") + Path.DirectorySeparatorChar);
            string redistListDirectory = Path.Combine(framework41Directory, "RedistList");
            string redistListFile = Path.Combine(redistListDirectory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(framework41Directory);
                Directory.CreateDirectory(redistListDirectory);

                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' Name='.NET Framework 4.1'>" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistListFile, redistListContents);

                string targetFrameworkMoniker = "MyFramework, Version=v4.1";
                MockEngine engine = new MockEngine();
                GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
                getReferencePaths.BuildEngine = engine;
                getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
                getReferencePaths.RootPath = tempDirectory;
                getReferencePaths.Execute();
                string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
                string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
                Assert.Equal(1, returnedPaths.Length);
                Assert.True(returnedPaths[0].Equals(framework41Directory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, engine.Log.Length); // "Expected the log to contain nothing"
                Assert.True(displayName.Equals(".NET Framework 4.1", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is a good target framework moniker passed in.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGoodWithRootWithProfile()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "TestGeneralFrameworkMonikerGoodWithRootWithProfile");
            string framework41Directory = Path.Combine(tempDirectory, Path.Combine("MyFramework", "v4.1", "Profile", "Client"));
            string redistListDirectory = Path.Combine(framework41Directory, "RedistList");
            string redistListFile = Path.Combine(redistListDirectory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(framework41Directory);
                Directory.CreateDirectory(redistListDirectory);

                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' Name='.NET Framework 4.1 Client'>" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistListFile, redistListContents);
                FrameworkNameVersioning name = new FrameworkNameVersioning("MyFramework", new Version("4.1"), "Client");
                string targetFrameworkMoniker = name.FullName;
                MockEngine engine = new MockEngine();
                GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
                getReferencePaths.BuildEngine = engine;
                getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
                getReferencePaths.RootPath = tempDirectory;
                getReferencePaths.Execute();
                string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
                string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
                Assert.Equal(1, returnedPaths.Length);
                Assert.True(returnedPaths[0].Equals(framework41Directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                Assert.True(displayName.Equals(".NET Framework 4.1 Client", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
                }
            }
        }

        /// <summary>
        /// Test the case where the target framework moniker is null. Expect there to be an error logged.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerNull()
        {
            MockEngine engine = new MockEngine();
            GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
            getReferencePaths.BuildEngine = engine;
            getReferencePaths.TargetFrameworkMoniker = null;
            getReferencePaths.Execute();
            string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
            Assert.Null(getReferencePaths.TargetFrameworkMonikerDisplayName);
            Assert.Equal(0, returnedPaths.Length);
            Assert.Equal(1, engine.Errors);
        }

        /// <summary>
        /// Test the case where the target framework moniker is empty. Expect there to be an error logged.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerNonExistent()
        {
            MockEngine engine = new MockEngine();
            GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
            getReferencePaths.BuildEngine = engine;
            // Make a framework which does not exist, intentional misspelling of framework
            getReferencePaths.TargetFrameworkMoniker = ".NetFramewok, Version=v99.0";
            bool success = getReferencePaths.Execute();
            Assert.False(success);
            string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
            Assert.Equal(0, returnedPaths.Length);
            string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
            Assert.Null(displayName);
            FrameworkNameVersioning frameworkMoniker = new FrameworkNameVersioning(getReferencePaths.TargetFrameworkMoniker);
            string message = ResourceUtilities.FormatResourceString("GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound", frameworkMoniker.ToString());
            engine.AssertLogContains("ERROR MSB3644: " + message);
        }

        [Fact]
        public void TestSuppressNotFoundError()
        {
            MockEngine engine = new MockEngine();
            GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
            getReferencePaths.BuildEngine = engine;
            // Make a framework which does not exist, intentional misspelling of framework
            getReferencePaths.TargetFrameworkMoniker = ".NetFramewok, Version=v99.0";
            getReferencePaths.SuppressNotFoundError = true;
            bool success = getReferencePaths.Execute();
            Assert.True(success);
            string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
            Assert.Equal(0, returnedPaths.Length);
            string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
            Assert.Null(displayName);
            Assert.Equal(0, engine.Errors);
        }

        /// <summary>
        /// Test the case where there is a good target framework moniker passed in.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGoodWithInvalidIncludePath()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "TestGeneralFrameworkMonikerGoodWithInvalidIncludePath");
            string framework41Directory = Path.Combine(tempDirectory, Path.Combine("MyFramework", "v4.1") + Path.DirectorySeparatorChar);
            string redistListDirectory = Path.Combine(framework41Directory, "RedistList");
            string redistListFile = Path.Combine(redistListDirectory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(framework41Directory);
                Directory.CreateDirectory(redistListDirectory);

                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' IncludeFramework='..\\Mooses' Name='Chained oh noes'>" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistListFile, redistListContents);

                string targetFrameworkMoniker = "MyFramework, Version=v4.1";
                MockEngine engine = new MockEngine();
                GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
                getReferencePaths.BuildEngine = engine;
                getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
                getReferencePaths.RootPath = tempDirectory;
                getReferencePaths.Execute();
                string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
                Assert.Equal(0, returnedPaths.Length);
                string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
                Assert.Null(displayName);
                FrameworkNameVersioning frameworkMoniker = new FrameworkNameVersioning(getReferencePaths.TargetFrameworkMoniker);
                string message = ResourceUtilities.FormatResourceString("GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound", frameworkMoniker.ToString());
                engine.AssertLogContains(message);
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is a good target framework moniker passed in but there is a problem with the RedistList.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGoodWithInvalidCharInIncludePath()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "TestGeneralFrameworkMonikerGoodWithInvalidCharInIncludePath");
            string framework41Directory = Path.Combine(tempDirectory, Path.Combine("MyFramework", "v4.1") + Path.DirectorySeparatorChar);
            string redistListDirectory = Path.Combine(framework41Directory, "RedistList");
            string redistListFile = Path.Combine(redistListDirectory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(framework41Directory);
                Directory.CreateDirectory(redistListDirectory);

                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' IncludeFramework='v4.*' Name='Chained oh noes'>" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                File.WriteAllText(redistListFile, redistListContents);

                string targetFrameworkMoniker = "MyFramework, Version=v4.1";
                MockEngine engine = new MockEngine();
                GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
                getReferencePaths.BuildEngine = engine;
                getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
                getReferencePaths.RootPath = tempDirectory;
                getReferencePaths.Execute();
                string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
                Assert.Equal(0, returnedPaths.Length);
                string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
                Assert.Null(displayName);
                FrameworkNameVersioning frameworkMoniker = new FrameworkNameVersioning(getReferencePaths.TargetFrameworkMoniker);
                if (NativeMethodsShared.IsWindows)
                {
                    engine.AssertLogContains("MSB3643");
                }
                else
                {
                    // Since under Unix there are no invalid characters, we don't fail in the incorrect path
                    // and go through to actually looking for the directory
                    string message =
                        ResourceUtilities.FormatResourceString(
                            "GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound",
                            frameworkMoniker.ToString());
                    engine.AssertLogContains(message);
                }
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    FileUtilities.DeleteWithoutTrailingBackslash(framework41Directory, true);
                }
            }
        }

        /// <summary>
        /// Test the case where there is a good target framework moniker passed in.
        /// </summary>
        [Fact]
        public void TestGeneralFrameworkMonikerGoodWithFrameworkInFallbackPaths()
        {
            using (var env = TestEnvironment.Create())
            {
                string frameworkRootDir = Path.Combine(env.DefaultTestDirectory.FolderPath, "framework-root");
                var framework41Directory = env.CreateFolder(Path.Combine(frameworkRootDir, Path.Combine("MyFramework", "v4.1") + Path.DirectorySeparatorChar));
                var redistListDirectory = env.CreateFolder(Path.Combine(framework41Directory.FolderPath, "RedistList"));

                string redistListContents =
                        "<FileList Redist='Microsoft-Windows-CLRCoreComp' Name='.NET Framework 4.1'>" +
                            "<File AssemblyName='System.Xml' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                             "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                        "</FileList >";

                env.CreateFile(redistListDirectory, "FrameworkList.xml", redistListContents);

                string targetFrameworkMoniker = "MyFramework, Version=v4.1";
                MockEngine engine = new MockEngine();
                GetReferenceAssemblyPaths getReferencePaths = new GetReferenceAssemblyPaths();
                getReferencePaths.BuildEngine = engine;
                getReferencePaths.TargetFrameworkMoniker = targetFrameworkMoniker;
                getReferencePaths.RootPath = env.CreateFolder().FolderPath;
                getReferencePaths.RootPath = frameworkRootDir;
                getReferencePaths.TargetFrameworkFallbackSearchPaths = $"/foo/bar;{frameworkRootDir}";
                getReferencePaths.Execute();
                string[] returnedPaths = getReferencePaths.ReferenceAssemblyPaths;
                string displayName = getReferencePaths.TargetFrameworkMonikerDisplayName;
                Assert.Equal(1, returnedPaths.Length);
                Assert.True(returnedPaths[0].Equals(framework41Directory.FolderPath, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, engine.Log.Length); // "Expected the log to contain nothing"
                Assert.True(displayName.Equals(".NET Framework 4.1", StringComparison.OrdinalIgnoreCase));
            }
        }

    }
}
#endif
