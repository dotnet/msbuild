using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AppConfig
{
    public sealed class StronglyNamedDependencyAppConfig : ResolveAssemblyReferenceTestFixture
    {
        public StronglyNamedDependencyAppConfig(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        new internal string[] DefaultPaths
            {
                get { return new string[] { s_myApp_V05Path, s_myApp_V10Path, s_myComponentsV05Path, s_myComponentsV10Path, s_myComponentsV20Path, s_myComponentsV30Path }; }
        }


        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// Expected:
        /// - The resulting UnifyMe returned should be 2.0.0.0.
        /// Rationale:
        /// Strongly named dependencies should unify according to the bindingRedirects in the app.config.
        /// </summary>
        [Fact]
        public void Exists()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            Assert.Single(t.ResolvedDependencyFiles);
            t.ResolvedDependencyFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll"))
                );

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes UnifyMe version from 1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// -Version 2.0.0.0 of UnifyMe is in the Black List
        /// Expected:
        /// - There should be a warning indicating that DependsOnUnified has a dependency UnifyMe 2.0.0.0 which is not in a TargetFrameworkSubset.
        /// - There will be no unified message.
        /// Rationale:
        /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, if the unified version is in the black list it should be removed and warned.
        /// </summary>
        [Fact]
        public void ExistsPromotedDependencyInTheBlackList()
        {
            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
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
                appConfigFile = WriteAppConfig
                    (
                        "        <dependentAssembly>\n" +
                        "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                        "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                        "        </dependentAssembly>\n"
                    );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t, false);

                Assert.True(succeeded);
                Assert.Empty(t.ResolvedDependencyFiles);
                engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll"))
                    );
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
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes a *different* assembly version name from
        //    1.0.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 1.0.0.0.
        /// Rationale:
        /// An unrelated bindingRedirect in the app.config should have no bearing on unification
        /// of another file.
        /// </summary>
        [Fact]
        public void ExistsDifferentName()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            Assert.Single(t.ResolvedDependencyFiles);
            t.ResolvedDependencyFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);

            // Cleanup.
            File.Delete(appConfigFile);
        }


        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes assembly version from range 0.0.0.0-1.5.0.0 to 2.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// -- The resulting assembly returned should be 2.0.0.0.
        /// Rationale:
        /// Strongly named dependencies should unify according to the bindingRedirects in the app.config, even
        /// if a range is involved.
        /// </summary>
        [Fact]
        public void ExistsOldVersionRange()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            Assert.Single(t.ResolvedDependencyFiles);
            t.ResolvedDependencyFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll"))
                );

            // Cleanup.
            File.Delete(appConfigFile);
        }


        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 1.0.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes assembly version from 1.0.0.0 to 4.0.0.0
        /// - Version 1.0.0.0 of the file exists.
        /// - Version 4.0.0.0 of the file *does not* exist.
        /// Expected:
        /// - The dependent assembly should be unresolved.
        /// Rationale:
        /// The fusion loader is going to want to respect the app.config file. There's no point in
        /// feeding it the wrong version.
        /// </summary>
        [Fact]
        public void HighVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            Assert.Empty(t.ResolvedDependencyFiles);
            string shouldContain;

            string code = t.Log.ExtractMessageCode
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.FailedToResolveReference"),
                        String.Format(AssemblyResources.GetString("General.CouldNotLocateAssembly"), "UNIFyMe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                    out shouldContain
                );


            engine.AssertLogContains
                (
                    shouldContain
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll"))
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UNIFyMe, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );



            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - A single reference to DependsOnUnified was passed in.
        ///   - This assembly depends on version 0.5.0.0 of UnifyMe.
        /// - An app.config was passed in that promotes assembly version from 0.0.0.0-2.0.0.0 to 2.0.0.0
        /// - Version 0.5.0.0 of the file *does not* exists.
        /// - Version 2.0.0.0 of the file exists.
        /// Expected:
        /// - The resulting assembly returned should be 2.0.0.0.
        /// Rationale:
        /// The lower (unified-from) version need not exist on disk (in fact we shouldn't even try to
        /// resolve it) in order to arrive at the correct answer.
        /// </summary>
        [Fact]
        public void LowVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            Assert.Single(t.ResolvedDependencyFiles);
            t.ResolvedDependencyFiles[0].GetMetadata("FusionName").ShouldBe("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", StringCompareShould.IgnoreCase);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "0.5.0.0", appConfigFile, Path.Combine(s_myApp_V05Path, "DependsOnUnified.dll"))
                );

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has some garbage in the version number.
        /// Expected:
        /// - An error and task failure.
        /// Rationale:
        /// Can't proceed with a bad app.config.
        /// </summary>
        [Fact]
        public void GarbageVersionInAppConfigFile()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='GarbledOldVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='Garbled' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has a missing oldVersion in a bindingRedirect.
        /// Expected:
        /// - An error and task failure.
        /// Rationale:
        /// Can't proceed with a bad app.config.
        /// </summary>
        [Fact]
        public void GarbageAppConfigMissingOldVersion()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='MissingOldVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("AppConfig.BindingRedirectMissingOldVersion"))
                );

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has a missing newVersion in a bindingRedirect.
        /// Expected:
        /// - An error and task failure.
        /// Rationale:
        /// Can't proceed with a bad app.config.
        /// </summary>
        [Fact]
        public void GarbageAppConfigMissingNewVersion()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='MissingNewVersion' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                    "            <bindingRedirect oldVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("AppConfig.BindingRedirectMissingNewVersion"))
                );

            // Cleanup.
            File.Delete(appConfigFile);
        }


        /// <summary>
        /// In this case,
        /// - An app.config is passed in that has some missing information in &lt;assemblyIdentity&gt; element.
        /// Expected:
        /// - An error and task failure.
        /// Rationale:
        /// Can't proceed with a bad app.config.
        /// </summary>
        [Fact]
        public void GarbageAppConfigAssemblyNameMissingPKTAndCulture()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Construct the app.config.
            string appConfigFile = WriteAppConfig
                (
                    "        <dependentAssembly>\n" +
                    "            <assemblyIdentity name='GarbledOldVersion' />\n" +
                    "            <bindingRedirect oldVersion='Garbled' newVersion='2.0.0.0' />\n" +
                    "        </dependentAssembly>\n"
                );

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = appConfigFile;

            bool succeeded = Execute(t);
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is specified
        /// *and*
        /// - AutoUnify=true.
        /// Expected:
        /// - Success.
        /// Rationale:
        /// With the introduction of the GenerateBindingRedirects task, RAR now accepts AutoUnify and App.Config at the same time.
        /// </summary>
        [Fact]
        public void AppConfigSpecifiedWhenAutoUnifyEqualsTrue()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

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
            t.AutoUnify = true;

            bool succeeded = Execute(t);

            // With the introduction of GenerateBindingRedirects task, RAR now accepts AutoUnify and App.Config at the same time.
            Assert.True(succeeded);
            Assert.Equal(0, engine.Errors);

            // Cleanup.
            File.Delete(appConfigFile);
        }

        /// <summary>
        /// In this case,
        /// - An app.config is specified, but the file doesn't exist.
        /// Expected:
        /// - An error and task failure.
        /// Rationale:
        /// App.config must exist if specified.
        /// </summary>
        [Fact]
        public void AppConfigDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine(_output);

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AppConfigFile = @"C:\MyNonexistentFolder\MyNonExistentApp.config";

            bool succeeded = Execute(t);
            Assert.False(succeeded);
            Assert.Equal(1, engine.Errors);
        }
    }
}
