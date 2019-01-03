using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.Prerequisite
{
    public sealed class StronglyNamedDependency : ResolveAssemblyReferenceTestFixture
    {
        public StronglyNamedDependency(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        new internal string[] DefaultPaths
            {
                get { return new string[] { s_myApp_V10Path, @"C:\Framework\Whidbey", @"C:\Framework\Everett" }; }
            }

        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnEverettSystem was passed in.
        ///   - This assembly depends on version 1.0.5000.0 of System.DLL.
        /// - No app.config is passed in.
        /// - Version 1.0.5000.0 of System.dll exists.
        /// - Whidbey Version of System.dll exists.
        /// Expected:
        /// - The resulting System.dll returned should be Whidbey version.
        /// Rationale:
        /// We automatically unify FX dependencies.
        /// </summary>
        [Fact]
            [Trait("Category", "mono-osx-failing")]
        public void Exists()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing VersioningAndUnification.Prerequisite.StronglyNamedDependency.Exists() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.Equal(0, engine.Errors);
            Assert.Equal(0, engine.Warnings);
            t.ResolvedDependencyFiles[0].GetMetadata("FusionName")
                .ShouldBe("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey, StringCompareShould.IgnoreCase);

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByFrameworkRetarget"), "1.0.5000.0", Path.Combine(s_myApp_V10Path, "DependsOnEverettSystem.dll"))
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.NotCopyLocalBecausePrerequisite"))
                );

            t.ResolvedDependencyFiles[0].GetMetadata("CopyLocal").ShouldBe("false", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnEverettSystem was passed in.
        ///   - This assembly depends on version 1.0.5000.0 of System.DLL.
        /// - No app.config is passed in.
        /// - Version 1.0.5000.0 of System.dll exists.
        /// - Whidbey Version of System.dll *does not* exist.
        /// Expected:
        /// - This should be an unresolved reference, we shouldn't fallback to the old version.
        /// Rationale:
        /// The fusion loader is going to want to respect the unified-to assembly. There's no point in
        /// feeding it the wrong version, and the drawback is that builds would be different from 
        /// machine-to-machine.
        /// </summary>
        [Fact]
        public void HighVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnEverettSystem, Version=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
                t.SearchPaths = new string[] { s_myApp_V10Path, @"C:\Framework\Everett" }; ;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Empty(t.ResolvedDependencyFiles);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByFrameworkRetarget"), "1.0.5000.0", Path.Combine(s_myApp_V10Path, "DependsOnEverettSystem.dll"))
                );
        }

        [Fact]
        public void VerifyAssemblyPulledOutOfFrameworkDoesntGetFrameworkFileAttribute()
        {
            MockEngine e = new MockEngine(_output);

                string actualFrameworkDirectory = s_myVersion20Path;
                string alternativeFrameworkDirectory = s_myVersion40Path;

            ITaskItem[] items = new TaskItem[] { new TaskItem(Path.Combine(actualFrameworkDirectory, "System.dll")) };

            // Version and directory match framework - it is a framework assembly
            string redistString1 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                   "<File AssemblyName='System' Version='2.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                   "</FileList >";

            ResolveAssemblyReference t1 = new ResolveAssemblyReference();
            t1.TargetFrameworkVersion = "v4.5";
            t1.TargetFrameworkDirectories = new string[] { actualFrameworkDirectory };
            ExecuteRAROnItemsAndRedist(t1, e, items, redistString1, true, new List<string>() { "{RawFileName}" });

            Assert.False(String.IsNullOrEmpty(t1.ResolvedFiles[0].GetMetadata("FrameworkFile")));

            // Higher version than framework, but directory matches - it is a framework assembly
            string redistString2 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                   "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                   "</FileList >";

            ResolveAssemblyReference t2 = new ResolveAssemblyReference();
            t2.TargetFrameworkVersion = "v4.5";
            t2.TargetFrameworkDirectories = new string[] { actualFrameworkDirectory };
            ExecuteRAROnItemsAndRedist(t2, e, items, redistString2, true, new List<string>() { "{RawFileName}" });

            Assert.False(String.IsNullOrEmpty(t2.ResolvedFiles[0].GetMetadata("FrameworkFile")));

            // Version is lower but directory does not match - it is a framework assembly
            string redistString3 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                   "<File AssemblyName='System' Version='3.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                   "</FileList >";

            ResolveAssemblyReference t3 = new ResolveAssemblyReference();
            t3.TargetFrameworkVersion = "v4.5";
            t3.TargetFrameworkDirectories = new string[] { alternativeFrameworkDirectory };
            ExecuteRAROnItemsAndRedist(t3, e, items, redistString3, true, new List<string>() { "{RawFileName}" });

            Assert.False(String.IsNullOrEmpty(t3.ResolvedFiles[0].GetMetadata("FrameworkFile")));

            // Version is higher and directory does not match - this assembly has been pulled out of .NET
            string redistString4 = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                   "<File AssemblyName='System' Version='1.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                   "</FileList >";

            ResolveAssemblyReference t4 = new ResolveAssemblyReference();
            t4.TargetFrameworkVersion = "v4.5";
            t4.TargetFrameworkDirectories = new string[] { alternativeFrameworkDirectory };
            ExecuteRAROnItemsAndRedist(t4, e, items, redistString4, true, new List<string>() { "{RawFileName}" });

            Assert.True(String.IsNullOrEmpty(t4.ResolvedFiles[0].GetMetadata("FrameworkFile")));
        }
    }
}
