// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AppConfig
{
    public sealed class FilePrimary : ResolveAssemblyReferenceTestFixture
    {
        public FilePrimary(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
        /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 2.0.0.0
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
        public void Exists()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing VersioningAndUnification.AppConfig.FilePrimary.Exists() test");

            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
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
        /// Test the case where the appconfig has a malformed binding redirect version.
        /// </summary>
        [Fact]
        public void BadAppconfigOldVersion()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };


            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "    <runtime>\n" +
                    "<assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                    "<dependentAssembly>\n" +
                    "<assemblyIdentity name='Micron.Facilities.Data' publicKeyToken='2D8C82D3A1452EF1' culture='neutral'/>\n" +
                    "<bindingRedirect oldVersion='1.*' newVersion='2.0.0.0'/>\n" +
                    "</dependentAssembly>\n" +
                    "</assemblyBinding>\n" +
                    "</runtime>\n");

            try
            {
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.False(succeeded);
                engine.AssertLogContains("MSB3249");
            }
            finally
            {
                if (File.Exists(appConfigFile))
                {
                    // Cleanup.
                    File.Delete(appConfigFile);
                }
            }
        }

        /// <summary>
        /// Test the case where the appconfig has a malformed binding redirect version.
        /// </summary>
        [Fact]
        public void BadAppconfigNewVersion()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };


            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "    <runtime>\n" +
                    "<assemblyBinding xmlns='urn:schemas-microsoft-com:asm.v1'>\n" +
                    "<dependentAssembly>\n" +
                    "<assemblyIdentity name='Micron.Facilities.Data' publicKeyToken='2D8C82D3A1452EF1' culture='neutral'/>\n" +
                    "<bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.*.0'/>\n" +
                    "</dependentAssembly>\n" +
                    "</assemblyBinding>\n" +
                    "</runtime>\n");

            try
            {
                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.BuildEngine = engine;
                t.AssemblyFiles = assemblyFiles;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.False(succeeded);
                engine.AssertLogContains("MSB3249");
            }
            finally
            {
                if (File.Exists(appConfigFile))
                {
                    // Cleanup.
                    File.Delete(appConfigFile);
                }
            }
        }

        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// -Version 2.0.0.0 of UnifyMe is in the Deny List
        /// Expected:
        /// - There should be a warning indicating that DependsOnUnified has a dependency UnifyMe 2.0.0.0 which is not in a TargetFrameworkSubset.
        /// - There will be no unified message.
        /// Rationale:
        /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, if the unified version is in the deny list it should be removed and warned.
        /// </summary>
        [Fact]
        public void ExistsPromotedDependencyInTheDenyList()
        {
            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFileName();
            string subsetListPath = FileUtilities.GetTemporaryFileName();
            string appConfigFile = null;
            try
            {
                File.WriteAllText(redistListPath, implicitRedistListContents);
                File.WriteAllText(subsetListPath, engineOnlySubset);


                // Create the engine.
                MockEngine engine = new MockEngine(_output);

                ITaskItem[] assemblyNames = new TaskItem[]
                {
                    new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };

                // Construct the app.config.
                appConfigFile = WriteAppConfig(
                        "        <dependentAssembly>\n" +
                        "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                        "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                        "        </dependentAssembly>\n");

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Empty(t.ResolvedDependencyFiles);
                engine.AssertLogDoesntContain(
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll")));
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);

                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        /// <summary>
        /// In this case,
        /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
        /// - An app.config was passed in that promotes a *different* assembly version name from 
        ///   1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// - The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// One entry in the app.config file should not be able to impact the mapping of an assembly
        /// with a different name.
        /// </summary>
        [Fact]
        public void ExistsDifferentName()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='DontUnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
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
        /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
        /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// - The resulting assembly returned should be 2.0.0.0.
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

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-1.5.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
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
        /// - A single primary file reference to assembly version 1.0.0.0 was passed in.
        /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 4.0.0.0 of the file *does not* exist.
        /// Expected:
        /// -- The resulting assembly returned should be 2.0.0.0.
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

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V10Path)
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='1.0.0.0' newVersion='4.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
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
        /// - A single primary file reference to assembly version 0.5.0.0 was passed in.
        /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
        /// - Version 0.5.0.0 of the file *does not* exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 2.0.0.0.
        /// Rationale:
        /// There's no way for the resolve algorithm to determine that the file reference corresponds
        /// to a particular AssemblyName. Because of this, there's no way to determine that we want to 
        /// promote from 0.5.0.0 to 2.0.0.0. In this case, just use the assembly name that was passed in.
        /// </summary>
        [Fact]
        public void LowVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyFiles = new TaskItem[]
            {
                        new TaskItem(s_unifyMeDll_V05Path)
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig(
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='0.0.0.0-2.0.0.0' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n");

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.AssemblyFiles = assemblyFiles;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Single(t.ResolvedFiles);
            Assert.Equal(t.ResolvedFiles[0].ItemSpec, assemblyFiles[0].ItemSpec);


            // Cleanup.
            File.Delete(appConfigFile);
        }
    }
}
