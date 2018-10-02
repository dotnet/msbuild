// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

using NUnit.Framework;

using Microsoft.Build;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.UnitTests.OM.OrcasCompatibility
{
    /// <summary>
    /// Fixture Class for the v9 OM Public Interface Compatibility Tests. Toolset Class.
    /// Also see Toolset tests in the Project test class.
    /// </summary>
    [TestFixture]
    public sealed class Toolset_Tests
    {
        /// <summary>
        /// Toolset Test. Construct, null Tools version
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToolsetNullToolsVersion()
        {
            Toolset toolset = new Toolset(null, "c:\aPath");
        }

        /// <summary>
        /// Toolset Test. Construct, empty Tools version
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ToolsetEmptyToolsVersion()
        {
            Toolset toolset = new Toolset(String.Empty, "c:\aPath");
        }

        /// <summary>
        /// Toolset Test. Construct, empty Tools path
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ToolsetEmptyToolsPath()
        {
            Toolset toolset = new Toolset("toolset", String.Empty);
        }

        /// <summary>
        /// Toolset Test. Construct, null Tools path
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ToolsetNullToolsPath()
        {
            Toolset toolset = new Toolset("toolset", null);
        }

        /// <summary>
        /// Toolset Test. Construct, special chars in tools version
        /// </summary>
        [Test]
        public void ToolsetEscapedVersions()
        {
            string escaped = @"%25%2a%3f%40%24%28%29%3b\";
            string unescaped = @"%*?@$();\";
            Toolset toolsetEscaped = new Toolset(escaped, @"c:\aPath");
            Toolset toolsetUnescaped = new Toolset(unescaped, @"c:\aPath");
            Assertion.AssertEquals(escaped, toolsetEscaped.ToolsVersion);
            Assertion.AssertEquals(unescaped, toolsetUnescaped.ToolsVersion);
        }

        /// <summary>
        /// Toolset Test. Load toolset from scalar does not evaluate
        /// </summary>
        [Test]
        public void ToolsetScalar()
        {
            Project p = new Project();
            p.SetProperty("version", "number");
            BuildProperty versionProperty = CompatibilityTestHelpers.FindBuildProperty(p, "version");
            p.ParentEngine.Toolsets.Add(new Toolset("scalar", @"$(version)"));
            object o = p.EvaluatedProperties;
            Assertion.AssertEquals(false, Object.Equals(versionProperty.Value,  p.ParentEngine.Toolsets["scalar"].ToolsVersion));
        }

        /// <summary>
        /// Toolset Test. Construct, overlong path
        /// </summary>
        [Test]
        [ExpectedException(typeof(PathTooLongException))]
        public void ToolsetLongPath()
        {
            string longPath = CompatibilityTestHelpers.GenerateLongPath(256);
            Toolset toolset = new Toolset("toolset", longPath);
        }

        /// <summary>
        /// Toolset Test. Construct, invalid path
        /// </summary>
        [Test]
        public void ToolsetInvalidPath()
        {
            string invalidPath = @"c:\invalid|path";
            Toolset toolset = new Toolset("toolset", invalidPath);
        }

        /// <summary>
        /// Toolset Test. Construct, import build properties, also tests getting BuildPropertGroups
        /// </summary>
        [Test]
        public void ToolsetImportProperties()
        {
            BuildPropertyGroup buildPropertyGroup = new BuildPropertyGroup();
            buildPropertyGroup.SetProperty("n", "v");
            Toolset toolset = new Toolset("toolversion", "c:\aPath", buildPropertyGroup);
            Assertion.AssertEquals(1, toolset.BuildProperties.Count);
        }

        /// <summary>
        /// Toolset Test. Construct, import null build properties
        /// </summary>
        [Test]
        public void ToolsetImportPropertiesNull()
        {           
            BuildPropertyGroup buildPropertyGroup = null;
            Toolset toolset = new Toolset("toolversion", "c:\aPath", buildPropertyGroup);
            Assertion.AssertEquals(0, toolset.BuildProperties.Count);
        }

        /// <summary>
        /// Toolset Test. Enforce a deep clone, clone properties and their groups too.
        /// </summary>
        [Test]
        public void ToolsetClone()
        {
            Toolset toolset = new Toolset("toolversion", "c:\aPath");
            toolset.BuildProperties.SetProperty("n", "v");
            Toolset toolset2 = toolset.Clone();
            Assertion.AssertEquals(false, object.ReferenceEquals(toolset, toolset2));
            Assertion.AssertEquals(false, object.ReferenceEquals(toolset.BuildProperties["n"], toolset2.BuildProperties["n"]));
            Assertion.AssertEquals(false, object.ReferenceEquals(toolset.BuildProperties, toolset2.BuildProperties));
            Assertion.AssertEquals(toolset.ToolsPath, toolset2.ToolsPath);
            Assertion.AssertEquals(toolset.ToolsVersion, toolset2.ToolsVersion);
        }

        /// <summary>
        /// Toolset Test. Get version
        /// </summary>
        [Test]
        public void ToolVersionGet()
        {
            Toolset toolset = new Toolset("toolversion", "c:\aPath");
            Assertion.AssertEquals("toolversion", toolset.ToolsVersion);
        }

        /// <summary>
        /// Toolset Test. Get Path (note stripping of the last slash) 
        /// </summary>
        [Test]
        public void ToolPathGetTrailingSlash()
        {
            Toolset toolset = new Toolset("toolversion", @"c:\aPath\");
            Assertion.AssertEquals(@"c:\aPath", toolset.ToolsPath);
        }

        /// <summary>
        /// Toolset Test. Get Path (root path, don't strip slash) 
        /// </summary>
        [Test]
        public void ToolPathGetRootPath()
        {
            Toolset toolset = new Toolset("toolversion", @"c:\");
            Assertion.AssertEquals(@"c:\", toolset.ToolsPath);
        }

        /// <summary>
        /// Toolset Test. Set special properties bofore add
        /// </summary>
        [Test]
        public void ToolVersionPropertiesSpecialProperties()
        {
            Engine e = new Engine();
            Toolset toolset = new Toolset("toolversion", "c:\aPath");
            toolset.BuildProperties.SetProperty("msbuildpath", "newValue");
            toolset.BuildProperties.SetProperty("msbuildtoolspath", "newValue");
            e.Toolsets.Add(toolset);
            Assertion.AssertEquals("newValue", e.Toolsets["toolversion"].BuildProperties["msbuildpath"].Value);
            Assertion.AssertEquals("newValue", e.Toolsets["toolversion"].BuildProperties["msbuildtoolspath"].Value);
        }

        /// <summary>
        /// Toolset Test. Cannot set a property on toolset after adding it to the eninge
        /// </summary>
        [Test]
        public void ToolVersionPropertiesSetafterAddingToolset()
        {
            Engine e = new Engine();
            Toolset toolset = new Toolset("toolversion", "c:\aPath");
            e.Toolsets.Add(toolset);
            e.Toolsets["toolversion"].BuildProperties.SetProperty("n", "v");
            e.Toolsets.Add(toolset);
            Assertion.AssertNull(e.Toolsets["toolversion"].BuildProperties["n"]);
        }
    }
}
