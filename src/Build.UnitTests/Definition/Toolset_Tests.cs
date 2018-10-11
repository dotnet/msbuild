// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

namespace Microsoft.Build.UnitTests.Definition
{
    public class Toolset_Tests
    {
        [Fact]
        public void ToolsetCtorErrors1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Toolset t = new Toolset(null, "x", new ProjectCollection(), null);
            }
           );
        }

        [Fact]
        public void ToolsetCtorErrors2()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Toolset t = new Toolset("x", null, new ProjectCollection(), null);
            }
           );
        }

        [Fact]
        public void ToolsetCtorErrors3()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Toolset t = new Toolset(String.Empty, "x", new ProjectCollection(), null);
            }
           );
        }

        [Fact]
        public void Regress27993_TrailingSlashTrimmedFromMSBuildToolsPath()
        {
            Toolset t;

            if (NativeMethodsShared.IsWindows)
            {
                t = new Toolset("x", "C:", new ProjectCollection(), null);
                Assert.Equal(@"C:", t.ToolsPath);
                t = new Toolset("x", @"C:\", new ProjectCollection(), null);
                Assert.Equal(@"C:\", t.ToolsPath);
                t = new Toolset("x", @"C:\\", new ProjectCollection(), null);
                Assert.Equal(@"C:\", t.ToolsPath);

                t = new Toolset("x", @"C:\foo", new ProjectCollection(), null);
                Assert.Equal(@"C:\foo", t.ToolsPath);
                t = new Toolset("x", @"C:\foo\", new ProjectCollection(), null);
                Assert.Equal(@"C:\foo", t.ToolsPath);
                t = new Toolset("x", @"C:\foo\\", new ProjectCollection(), null);
                Assert.Equal(@"C:\foo\", t.ToolsPath); // trim at most one slash

                t = new Toolset("x", @"\\foo\share", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share", t.ToolsPath);
                t = new Toolset("x", @"\\foo\share\", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share", t.ToolsPath);
                t = new Toolset("x", @"\\foo\share\\", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share\", t.ToolsPath); // trim at most one slash
            }
            else
            {
                t = new Toolset("x", "/", new ProjectCollection(), null);
                Assert.Equal("/", t.ToolsPath);

                t = new Toolset("x", "/foo", new ProjectCollection(), null);
                Assert.Equal("/foo", t.ToolsPath);
                t = new Toolset("x", "/foo/", new ProjectCollection(), null);
                Assert.Equal(@"/foo", t.ToolsPath);
                t = new Toolset("x", "/foo//", new ProjectCollection(), null);
                Assert.Equal("/foo/", t.ToolsPath); // trim at most one slash

                t = new Toolset("x", @"\\foo\share", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share", t.ToolsPath);
                t = new Toolset("x", @"\\foo\share/", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share", t.ToolsPath);
                t = new Toolset("x", @"\\foo\share//", new ProjectCollection(), null);
                Assert.Equal(@"\\foo\share/", t.ToolsPath); // trim at most one slash
            }
        }

        [Fact]
        public void ValidateToolsetTranslation()
        {
            PropertyDictionary<ProjectPropertyInstance> buildProperties = new PropertyDictionary<ProjectPropertyInstance>();
            buildProperties.Set(ProjectPropertyInstance.Create("a", "a1"));

            PropertyDictionary<ProjectPropertyInstance> environmentProperties = new PropertyDictionary<ProjectPropertyInstance>();
            environmentProperties.Set(ProjectPropertyInstance.Create("b", "b1"));

            PropertyDictionary<ProjectPropertyInstance> globalProperties = new PropertyDictionary<ProjectPropertyInstance>();
            globalProperties.Set(ProjectPropertyInstance.Create("c", "c1"));

            PropertyDictionary<ProjectPropertyInstance> subToolsetProperties = new PropertyDictionary<ProjectPropertyInstance>();
            subToolsetProperties.Set(ProjectPropertyInstance.Create("d", "d1"));

            Dictionary<string, SubToolset> subToolsets = new Dictionary<string, SubToolset>(StringComparer.OrdinalIgnoreCase);
            subToolsets.Add("dogfood", new SubToolset("dogfood", subToolsetProperties));

            Toolset t = new Toolset("4.0", "c:\\bar", buildProperties, environmentProperties, globalProperties,
                subToolsets, "c:\\foo", "4.0", new Dictionary<string, ProjectImportPathMatch>
                {
                    ["MSBuildExtensionsPath"] = new ProjectImportPathMatch("MSBuildExtensionsPath", new List<string> {@"c:\foo"})
                });

            ((INodePacketTranslatable)t).Translate(TranslationHelpers.GetWriteTranslator());
            Toolset t2 = Toolset.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(t.ToolsVersion, t2.ToolsVersion);
            Assert.Equal(t.ToolsPath, t2.ToolsPath);
            Assert.Equal(t.OverrideTasksPath, t2.OverrideTasksPath);
            Assert.Equal(t.Properties.Count, t2.Properties.Count);

            foreach (string key in t.Properties.Values.Select(p => p.Name))
            {
                Assert.Equal(t.Properties[key].Name, t2.Properties[key].Name);
                Assert.Equal(t.Properties[key].EvaluatedValue, t2.Properties[key].EvaluatedValue);
            }

            Assert.Equal(t.SubToolsets.Count, t2.SubToolsets.Count);

            foreach (string key in t.SubToolsets.Keys)
            {
                SubToolset subToolset1 = t.SubToolsets[key];
                SubToolset subToolset2 = null;

                if (t2.SubToolsets.TryGetValue(key, out subToolset2))
                {
                    Assert.Equal(subToolset1.SubToolsetVersion, subToolset2.SubToolsetVersion);
                    Assert.Equal(subToolset1.Properties.Count, subToolset2.Properties.Count);

                    foreach (string subToolsetPropertyKey in subToolset1.Properties.Values.Select(p => p.Name))
                    {
                        Assert.Equal(subToolset1.Properties[subToolsetPropertyKey].Name, subToolset2.Properties[subToolsetPropertyKey].Name);
                        Assert.Equal(subToolset1.Properties[subToolsetPropertyKey].EvaluatedValue, subToolset2.Properties[subToolsetPropertyKey].EvaluatedValue);
                    }
                }
                else
                {
                    Assert.True(false, $"Sub-toolset {key} was lost in translation.");
                }
            }

            Assert.Equal(t.DefaultOverrideToolsVersion, t2.DefaultOverrideToolsVersion);

            Assert.NotNull(t2.ImportPropertySearchPathsTable);
            Assert.Equal(1, t2.ImportPropertySearchPathsTable.Count);
            Assert.Equal(@"c:\foo", t2.ImportPropertySearchPathsTable["MSBuildExtensionsPath"].SearchPaths[0]);
        }

        [Fact]
        public void TestDefaultSubToolset()
        {
            Toolset t = GetFakeToolset(null /* no global properties */);

            // The highest one numerically -- in this case, v13.
            Assert.Equal("v13.0", t.DefaultSubToolsetVersion);
        }

        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TestDefaultSubToolsetFor40()
        {
            Toolset t = ProjectCollection.GlobalProjectCollection.GetToolset("4.0");

            if (t != null)
            {
                if (Toolset.Dev10IsInstalled)
                {
                    // If Dev10 is installed, the default sub-toolset = no sub-toolset
                    Assert.Equal(Constants.Dev10SubToolsetValue, t.DefaultSubToolsetVersion);
                }
                else
                {
                    // Otherwise, it's the highest one numerically.  Since by definition if Dev10 isn't 
                    // installed and subtoolsets exists we must be at least Dev11, it should be "11.0" 
                    Assert.Equal("11.0", t.DefaultSubToolsetVersion);
                }
            }
        }

        [Fact]
        public void TestDefaultWhenNoSubToolset()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                ProjectCollection projectCollection = new ProjectCollection();
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                if (Toolset.Dev10IsInstalled)
                {
                    Assert.Equal(Constants.Dev10SubToolsetValue, t.DefaultSubToolsetVersion);
                }
                else
                {
                    Assert.Null(t.DefaultSubToolsetVersion);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersionWhenNoSubToolset()
        {
            if (NativeMethodsShared.IsUnixLike)
            {
                return; // "TODO: Under Unix this test throws. Investigate"
            }

            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                ProjectCollection projectCollection = new ProjectCollection();
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                string subToolsetVersion = t.GenerateSubToolsetVersion();

                if (Toolset.Dev10IsInstalled)
                {
                    Assert.Equal(Constants.Dev10SubToolsetValue, subToolsetVersion);
                }
                else
                {
                    Assert.Null(subToolsetVersion);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestNoSubToolset_GlobalPropertyOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", "99.0");

                ProjectCollection projectCollection = new ProjectCollection(globalProperties);
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                Assert.Equal("99.0", t.GenerateSubToolsetVersion());
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestNoSubToolset_EnvironmentOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "foo");

                ProjectCollection projectCollection = new ProjectCollection();
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                Assert.Equal("foo", t.GenerateSubToolsetVersion());
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestNoSubToolset_ExplicitlyPassedGlobalPropertyOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                ProjectCollection projectCollection = new ProjectCollection();
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", "v14.0");

                Assert.Equal("v14.0", t.GenerateSubToolsetVersion(globalProperties, 0));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestNoSubToolset_ExplicitlyPassedGlobalPropertyWins()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "foo");

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", "v13.0");

                ProjectCollection projectCollection = new ProjectCollection(globalProperties);
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                IDictionary<string, string> explicitGlobalProperties = new Dictionary<string, string>();
                explicitGlobalProperties.Add("VisualStudioVersion", "baz");

                Assert.Equal("baz", t.GenerateSubToolsetVersion(explicitGlobalProperties, 0));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersion_GlobalPropertyOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", ObjectModelHelpers.CurrentVisualStudioVersion);

                Toolset t = GetFakeToolset(globalProperties);

                Assert.Equal(ObjectModelHelpers.CurrentVisualStudioVersion, t.GenerateSubToolsetVersion());
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersion_EnvironmentOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", "FakeSubToolset");

                Toolset t = GetFakeToolset(null);

                Assert.Equal("FakeSubToolset", t.GenerateSubToolsetVersion());
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersion_ExplicitlyPassedGlobalPropertyOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                Toolset t = GetFakeToolset(null);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", "v13.0");

                Assert.Equal("v13.0", t.GenerateSubToolsetVersion(globalProperties, 0));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersion_SolutionVersionOverrides()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", null);

                Toolset t = GetFakeToolset(null);

                // VisualStudioVersion = SolutionVersion - 1
                Assert.Equal("12.0", t.GenerateSubToolsetVersion(null, 13));
                Assert.Equal("v13.0", t.GenerateSubToolsetVersion(null, 14));

                // however, if there is no matching solution version, we just fall back to the 
                // default sub-toolset. 
                Assert.Equal(t.DefaultSubToolsetVersion, t.GenerateSubToolsetVersion(null, 55));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGenerateSubToolsetVersion_ExplicitlyPassedGlobalPropertyWins()
        {
            string originalVisualStudioVersion = Environment.GetEnvironmentVariable("VisualStudioVersion");

            try
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", ObjectModelHelpers.CurrentVisualStudioVersion);

                IDictionary<string, string> globalProperties = new Dictionary<string, string>();
                globalProperties.Add("VisualStudioVersion", "v13.0");

                ProjectCollection projectCollection = new ProjectCollection(globalProperties);
                Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

                Toolset t = new Toolset("Fake", parentToolset.ToolsPath, null, projectCollection, null, parentToolset.OverrideTasksPath);

                IDictionary<string, string> explicitGlobalProperties = new Dictionary<string, string>();
                explicitGlobalProperties.Add("VisualStudioVersion", "FakeSubToolset");

                Assert.Equal("FakeSubToolset", t.GenerateSubToolsetVersion(explicitGlobalProperties, 0));
            }
            finally
            {
                Environment.SetEnvironmentVariable("VisualStudioVersion", originalVisualStudioVersion);
            }
        }

        [Fact]
        public void TestGetPropertyFromSubToolset()
        {
            Toolset t = GetFakeToolset(null);

            Assert.Equal("a1", t.GetProperty("a", "v11.0").EvaluatedValue); // property in base toolset
            Assert.Equal("c2", t.GetProperty("c", "v11.0").EvaluatedValue); // property in sub-toolset
            Assert.Equal("b2", t.GetProperty("b", "v11.0").EvaluatedValue); // property in sub-toolset that overrides base toolset
            Assert.Null(t.GetProperty("d", "v11.0")); // property in a different sub-toolset
        }

        /// <summary>
        /// Creates a standard ProjectCollection and adds a fake toolset with the following contents to it:  
        /// 
        /// ToolsVersion = Fake
        /// Base Properties: 
        /// a = a1
        /// b = b1
        /// 
        /// SubToolset "12.0": 
        /// d = d4
        /// e = e5
        /// 
        /// SubToolset "v11.0": 
        /// b = b2
        /// c = c2
        /// 
        /// SubToolset "FakeSubToolset":
        /// a = a3
        /// c = c3
        /// 
        /// SubToolset "v13.0":
        /// f = f6 
        /// g = g7
        /// </summary>
        private Toolset GetFakeToolset(IDictionary<string, string> globalPropertiesForProjectCollection)
        {
            ProjectCollection projectCollection = new ProjectCollection(globalPropertiesForProjectCollection);

            IDictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            properties.Add("a", "a1");
            properties.Add("b", "b1");

            Dictionary<string, SubToolset> subToolsets = new Dictionary<string, SubToolset>(StringComparer.OrdinalIgnoreCase);

            // SubToolset 12.0 properties
            PropertyDictionary<ProjectPropertyInstance> subToolset12Properties = new PropertyDictionary<ProjectPropertyInstance>();
            subToolset12Properties.Set(ProjectPropertyInstance.Create("d", "d4"));
            subToolset12Properties.Set(ProjectPropertyInstance.Create("e", "e5"));

            // SubToolset v11.0 properties
            PropertyDictionary<ProjectPropertyInstance> subToolset11Properties = new PropertyDictionary<ProjectPropertyInstance>();
            subToolset11Properties.Set(ProjectPropertyInstance.Create("b", "b2"));
            subToolset11Properties.Set(ProjectPropertyInstance.Create("c", "c2"));

            // FakeSubToolset properties
            PropertyDictionary<ProjectPropertyInstance> fakeSubToolsetProperties = new PropertyDictionary<ProjectPropertyInstance>();
            fakeSubToolsetProperties.Set(ProjectPropertyInstance.Create("a", "a3"));
            fakeSubToolsetProperties.Set(ProjectPropertyInstance.Create("c", "c3"));

            // SubToolset v13.0 properties
            PropertyDictionary<ProjectPropertyInstance> subToolset13Properties = new PropertyDictionary<ProjectPropertyInstance>();
            subToolset13Properties.Set(ProjectPropertyInstance.Create("f", "f6"));
            subToolset13Properties.Set(ProjectPropertyInstance.Create("g", "g7"));

            subToolsets.Add("12.0", new SubToolset("12.0", subToolset12Properties));
            subToolsets.Add("v11.0", new SubToolset("v11.0", subToolset11Properties));
            subToolsets.Add("FakeSubToolset", new SubToolset("FakeSubToolset", fakeSubToolsetProperties));
            subToolsets.Add("v13.0", new SubToolset("v13.0", subToolset13Properties));

            Toolset parentToolset = projectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion);

            Toolset fakeToolset = new Toolset("Fake", parentToolset.ToolsPath, properties, projectCollection, subToolsets, parentToolset.OverrideTasksPath);

            projectCollection.AddToolset(fakeToolset);

            return fakeToolset;
        }
    }
}
