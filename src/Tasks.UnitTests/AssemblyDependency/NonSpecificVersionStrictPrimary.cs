using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AppConfig
{
    public sealed class NonSpecificVersionStrictPrimary : ResolveAssemblyReferenceTestFixture
    {
        public NonSpecificVersionStrictPrimary(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        new internal string[] DefaultPaths
        {
            get { return new string[] { s_myComponentsV05Path, s_myComponentsV10Path, s_myComponentsV20Path, s_myComponentsV30Path }; }
        }


        /// <summary>
        /// In this case,
        /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
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
        public void Exists()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };
            assemblyNames[0].SetMetadata("SpecificVersion", "false");


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
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }



        /// <summary>
        /// In this case,
        /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
        /// - An app.config was passed in that promotes a *different* assembly version name from 
        //    1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// One entry in the app.config file should not be able to impact the mapping of an assembly
        /// with a different name.
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
            assemblyNames[0].SetMetadata("SpecificVersion", "false");

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
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }


        /// <summary>
        /// In this case,
        /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
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
            assemblyNames[0].SetMetadata("SpecificVersion", "false");

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
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary non-version-strict reference was passed in to assembly version 1.0.0.0
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
            assemblyNames[0].SetMetadata("SpecificVersion", "false");

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
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single primary non-version-strict reference was passed in to assembly version 0.5.0.0
        /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
        /// - Version 0.5.0.0 of the file *does not* exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 1.0.0.0 (remember this is non-version-strict)
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
            assemblyNames[0].SetMetadata("SpecificVersion", "false");

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
            Assert.Single(t.ResolvedFiles);
            t.ResolvedFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }
    }
}
