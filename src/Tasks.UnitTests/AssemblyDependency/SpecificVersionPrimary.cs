using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AppConfig
{
    public sealed class SpecificVersionPrimary : ResolveAssemblyReferenceTestFixture
    {
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
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void Exists()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing VersioningAndUnification.Prerequisite.SpecificVersionPrimary.Exists() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));
            AssertNoCase(@"{Registry:Software\Microsoft\.NetFramework,v2.0,AssemblyFoldersEx}", t.ResolvedFiles[0].GetMetadata("ResolvedFrom"));

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary version-strict reference was passed in to assembly version 1.0.0.0
        /// - An app.config was passed in that promotes a *different* assembly version name from 
        //    1.0.0.0 to 2.0.0.0
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
        [Trait("Category", "mono-osx-failing")]
        public void ExistsDifferentName()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

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
        [Trait("Category", "mono-osx-failing")]
        public void ExistsOldVersionRange()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

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
        [Trait("Category", "mono-osx-failing")]
        public void HighVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", t.ResolvedFiles[0].GetMetadata("FusionName"));

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
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "true");

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(0, t.ResolvedFiles.Length);

            // Cleanup.
            File.Delete(appConfigFile);
        }
    }
}