// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AppConfig
{
    public sealed class SpecificVersionPrimary : ResolveAssemblyReferenceTestFixture
    {
        public SpecificVersionPrimary(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// In this case,
        /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
        /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// - The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// Primary references are never unified. This is because:
        /// (a) The user expects that a primary reference will be respected.
        /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
        ///     dependencies anyway to make things work consistently. This would be a significant
        ///     perf hit when loading large solutions.
        /// </summary>
        [WindowsOnlyFact]
        public void Exists()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing VersioningAndUnification.Prerequisite.SpecificVersionPrimary.Exists() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);
            t.ResolvedFiles[0].GetMetadata("ResolvedFrom").ShouldBe(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// <list type="bullet">
        /// <item>A single primary version-strict reference was passed in to assembly version 1.0.0.0</item>
        /// <item>
        /// An app.config was passed in that promotes a *different* assembly version name from 
        /// 1.0.0.0 to 2.0.0.0
        /// </item>
        /// <item>Version 1.0.0.0 of the file exists.</item>
        /// <item>Version 2.0.0.0 of the file exists.</item>
        /// </list>
        /// Expected:
        /// <list type="bullet">
        /// <item>The resulting assembly returned should be 1.0.0.0.</item>
        /// </list>
        /// Rationale:
        /// Primary references are never unified. This is because:
        /// <list type="number">
        /// <item>
        /// The user expects that a primary reference will be respected.</item>
        /// <item>When FindDependencies is false and AutoUnify is true, we'd have to find all 
        /// dependencies anyway to make things work consistently. This would be a significant
        /// perf hit when loading large solutions.
        /// </item>
        /// </list>
        /// </summary>
        [Fact]
        public void ExistsDifferentName()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
        /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// Primary references are never unified. This is because:
        /// (a) The user expects that a primary reference will be respected.
        /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
        ///     dependencies anyway to make things work consistently. This would be a significant
        ///     perf hit when loading large solutions.
        /// </summary>
        [Fact]
        public void ExistsOldVersionRange()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
        /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 4.0.0.0 of the file *does not* exist.
        /// Expected:
        /// -- The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// Primary references are never unified. This is because:
        /// (a) The user expects that a primary reference will be respected.
        /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
        ///     dependencies anyway to make things work consistently. This would be a significant
        ///     perf hit when loading large solutions.
        /// </summary>
        [Fact]
        public void HighVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary version-strict reference was passed in to assembly version 0.5.0.0
        /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
        /// - Version 0.5.0.0 of the file *does not* exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// - The reference is not resolved.
        /// Rationale:
        /// Primary references are never unified--even those that don't exist on disk. This is because:
        /// (a) The user expects that a primary reference will be respected.
        /// (b) When FindDependencies is false and AutoUnify is true, we'd have to find all 
        ///     dependencies anyway to make things work consistently. This would be a significant
        ///     perf hit when loading large solutions.
        /// </summary>
        [Fact]
        public void LowVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Empty(t.ResolvedFiles);

            // Cleanup.
            File.Delete(appConfigFile);
        }
    }
}
