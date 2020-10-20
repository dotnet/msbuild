using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework
    /// </summary>
    public sealed class VerifyTargetFrameworkHigherThanRedist : ResolveAssemblyReferenceTestFixture
    {
        public VerifyTargetFrameworkHigherThanRedist(ITestOutputHelper output) : base(output)
        {
        }

        /// <summary>
        /// Verify there are no warnings when the assembly being resolved is not in the redist list and only has dependencies to references in the redist list with the same
        /// version as is described in the redist list.
        /// </summary>
        [Fact]
        public void TargetCurrentTargetFramework()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOnOnlyv4Assemblies.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// ReferenceVersion9 depends on mscorlib 9. However the redist list only allows 4.0 since framework unification for dependencies only
        /// allows upward unification this would result in a warning. Therefore we need to remap mscorlib 9 to 4.0
        ///
        /// </summary>
        [Fact]
        public void RemapAssemblyBasic()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9"),
                new TaskItem("DependsOnOnlyv4Assemblies"),
                new TaskItem("AnotherOne")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "<Remap>" +
                                  "<From AssemblyName='mscorlib' Version='9.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true'>" +
                                  "   <To AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  " </From>" +
                                  "<From AssemblyName='DependsOnOnlyv4Assemblies'>" +
                                  "   <To AssemblyName='ReferenceVersion9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' />" +
                                  " </From>" +
                                  "<From AssemblyName='AnotherOne'>" +
                                  "   <To AssemblyName='ReferenceVersion9' Version='9.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' />" +
                                  " </From>" +
                                  "</Remap>" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected NO warning in this scenario."
            e.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.RemappedReference", "DependsOnOnlyv4Assemblies", "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");
            e.AssertLogContainsMessageFromResource(resourceDelegate, "ResolveAssemblyReference.RemappedReference", "AnotherOne", "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089");

            Assert.Single(t.ResolvedFiles);

            Assert.Equal("AnotherOne", t.ResolvedFiles[0].GetMetadata("OriginalItemSpec"));

            Assert.Equal(Path.Combine(s_myComponentsMiscPath, "ReferenceVersion9.dll"), t.ResolvedFiles[0].ItemSpec);
        }

        /// <summary>
        /// Verify an error is emitted when the reference itself is in the redist list but is a higher version that is described in the redist list.
        /// In this case ReferenceVersion9 is version=9.0.0.0 but in the redist we show its highest version as 4.0.0.0.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistList()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='ReferenceVersion9' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains("MSB3257");
            e.AssertLogContains("ReferenceVersion9");
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Verify that if the reference that is higher than the highest version in the redist list is an MSBuild assembly, we do
        /// not warn -- this is a hack until we figure out how to properly deal with .NET assemblies being removed from the framework.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void HigherThanHighestInRedistListForMSBuildAssembly()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("Microsoft.Build")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='Microsoft.Build' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t1 = new ResolveAssemblyReference();
            t1.TargetFrameworkVersion = "v4.5";

            ExecuteRAROnItemsAndRedist(t1, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected successful resolution with no warnings."
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Single(t1.ResolvedFiles);

            ResolveAssemblyReference t2 = new ResolveAssemblyReference();
            t2.TargetFrameworkVersion = "v4.0";

            ExecuteRAROnItemsAndRedist(t2, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."

            // TODO: https://github.com/Microsoft/msbuild/issues/2305
            //e.AssertLogContains("Microsoft.Build.dll");
            Assert.Empty(t2.ResolvedFiles);

            ResolveAssemblyReference t3 = new ResolveAssemblyReference();
            t3.TargetFrameworkVersion = "v4.5";
            t3.UnresolveFrameworkAssembliesFromHigherFrameworks = true;

            ExecuteRAROnItemsAndRedist(t3, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."

            // TODO: https://github.com/Microsoft/msbuild/issues/2305
            // e.AssertLogContains("Microsoft.Build.dll");
            Assert.Single(t1.ResolvedFiles);
        }

        /// <summary>
        /// Expect no warning from a 3rd party redist list since they are not considered for multi targeting warnings.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistList3rdPartyRedist()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9")
            };

            string redistString = "<FileList Redist='MyRandomREdist' >" +
                                  "<File AssemblyName='mscorlib' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogDoesntContain("MSB3257");
            e.AssertLogContains("ReferenceVersion9");
            Assert.Single(t.ResolvedFiles);
        }

        /// <summary>
        /// Test the same case as above except for add the specific version metadata to ignore the warning.
        /// </summary>
        [Fact]
        public void HigherThanHighestInRedistListWithSpecificVersionMetadata()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='ReferenceVersion9' Version='4.0.0.0' PublicKeyToken='b17a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            e.AssertLogDoesntContain("MSB3257");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "ReferenceVersion9.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where the assembly itself is not in the redist list but it depends on an assembly which is in the redist list and is a higher version that what is listed in the redist list.
        /// In this case the assembly DependsOn9 depends on System 9.0.0.0 while the redist list only goes up to 4.0.0.0.
        /// </summary>
        [Fact]
        public void DependenciesHigherThanHighestInRedistList()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "<File AssemblyName='System.Data' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(2, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9", "System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Verify that if the reference that is higher than the highest version in the redist list is an MSBuild assembly, we do
        /// not warn -- this is a hack until we figure out how to properly deal with .NET assemblies being removed from the framework.
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        public void DependenciesHigherThanHighestInRedistListForMSBuildAssembly()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnMSBuild12")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='Microsoft.Build' Version='4.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t1 = new ResolveAssemblyReference();
            t1.TargetFrameworkVersion = "v5.0";

            ExecuteRAROnItemsAndRedist(t1, e, items, redistString, false);

            Assert.Equal(0, e.Warnings); // "Expected successful resolution with no warnings."
            e.AssertLogContains("DependsOnMSBuild12");
            e.AssertLogContains("Microsoft.Build.dll");
            Assert.Single(t1.ResolvedFiles);

            ResolveAssemblyReference t2 = new ResolveAssemblyReference();
            t2.TargetFrameworkVersion = "v4.0";

            ExecuteRAROnItemsAndRedist(t2, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario"
            e.AssertLogContains("DependsOnMSBuild12");

            // TODO: https://github.com/Microsoft/msbuild/issues/2305
            // e.AssertLogContains("Microsoft.Build.dll");
            Assert.Empty(t2.ResolvedFiles);

            ResolveAssemblyReference t3 = new ResolveAssemblyReference();
            //t2.TargetFrameworkVersion is null

            ExecuteRAROnItemsAndRedist(t3, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario"
            e.AssertLogContains("DependsOnMSBuild12");

            // TODO: https://github.com/Microsoft/msbuild/issues/2305
            // e.AssertLogContains("Microsoft.Build.dll");
            Assert.Empty(t3.ResolvedFiles);
        }

        /// <summary>
        /// Make sure when specific version is set to true and the dependencies of the reference are a higher version than what is in the redist list do not warn, do not unresolve
        /// </summary>
        [Fact]
        public void DependenciesHigherThanHighestInRedistListSpecificVersionMetadata()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                  "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "<File AssemblyName='System.Data' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            e.AssertLogDoesntContain("MSB3257");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where two assemblies depend on an assembly which is in the redist list but has a higher version than what is described in the redist list.
        /// DependsOn9 and DependsOn9Also both depend on System, Version=9.0.0.0 one of the items has the SpecificVersion metadata set. In this case
        /// we expect to only see a warning from one of the assemblies.
        /// </summary>
        [Fact]
        public void TwoDependenciesHigherThanHighestInRedistListIgnoreOnOne()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9Also")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(1, e.Warnings); // "Expected one warning in this scenario."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9Also", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to not find assembly, but did."
            Assert.False(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify the case where two assemblies depend on an assembly which is in the redist list but has a higher version than what is described in the redist list.
        /// DependsOn9 and DependsOn9Also both depend on System, Version=9.0.0.0. Both of the items has the specificVersion metadata set. In this case
        /// we expect to only see no warnings from the assemblies.
        /// </summary>
        [Fact]
        public void TwoDependenciesHigherThanHighestInRedistListIgnoreOnBoth()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");
            items[1].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, true);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            e.AssertLogDoesntContain("MSB3258");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Test the case where two assemblies with different versions but the same name depend on an assembly which is in the redist list but has a higher version than
        /// what is described in the redist list. We expect two warnings because both assemblies are going to be resolved even though one of them will not be copy local.
        /// </summary>
        [Fact]
        public void TwoDependenciesSameNameDependOnHigherVersion()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false);

            Assert.Equal(2, e.Warnings); // "Expected two warnings."
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            e.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", "DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089", "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "9.0.0.0", "4.0.0.0"));
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Empty(t.ResolvedFiles);
        }

        /// <summary>
        /// Test the case where the project has two references, one of them has dependencies which are contained within the projects target framework
        /// and there is another reference which has dependencies on a future framework (this is the light up scenario assembly).
        ///
        /// Make sure that if specific version is set on the lightup assembly that we do not unresolve it, and we also should not unify its dependencies.
        /// </summary>
        [Fact]
        public void MixedDependenciesSpecificVersionOnHigherVersionMetadataSet()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[1].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            List<string> additionalPaths = new List<string>();
            additionalPaths.Add(s_myComponents40ComponentPath);
            additionalPaths.Add(s_myVersion40Path);
            additionalPaths.Add(s_myVersion90Path + Path.DirectorySeparatorChar);


            ResolveAssemblyReference t = new ResolveAssemblyReference();
            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false, additionalPaths);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(2, t.ResolvedFiles.Length);
            Assert.Equal(2, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, s_40ComponentDependsOnOnlyv4AssembliesDllPath)); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Test the case where the project has two references, one of them has dependencies which are contained within the projects target framework
        /// and there is another reference which has dependencies on a future framework (this is the light up scenario assembly).
        ///
        /// Verify that if specific version is set on the other reference that we get the expected behavior:
        /// Un resolve the light up assembly.
        /// </summary>
        [Fact]
        public void MixedDependenciesSpecificVersionOnLowerVersionMetadataSet()
        {
            MockEngine e = new MockEngine(_output);

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089"),
                new TaskItem("DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089")
            };

            items[0].SetMetadata("SpecificVersion", "true");

            string redistString = "<FileList Redist='Microsoft-Windows-CLRCoreComp-Random' >" +
                                      "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='true' />" +
                                  "</FileList >";

            List<string> additionalPaths = new List<string>();
            additionalPaths.Add(s_myComponents40ComponentPath);
            additionalPaths.Add(s_myVersion40Path);
            additionalPaths.Add(s_myVersion90Path + Path.DirectorySeparatorChar);

            ResolveAssemblyReference t = new ResolveAssemblyReference();

            ExecuteRAROnItemsAndRedist(t, e, items, redistString, false, additionalPaths);

            Assert.Equal(1, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Single(t.ResolvedFiles);
            Assert.Single(t.ResolvedDependencyFiles);
            Assert.True(ContainsItem(t.ResolvedFiles, s_40ComponentDependsOnOnlyv4AssembliesDllPath)); // "Expected to find assembly, but didn't."
            Assert.False(ContainsItem(t.ResolvedFiles, Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll"))); // "Expected to find assembly, but didn't."
        }
    }
}
