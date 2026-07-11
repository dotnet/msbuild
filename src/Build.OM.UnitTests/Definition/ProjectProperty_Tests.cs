// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for ProjectProperty
    /// </summary>
    [TestClass]
    public class ProjectProperty_Tests
    {
        /// <summary>
        /// Project getter
        /// </summary>
        [MSBuildTestMethod]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v");

            Assert.IsTrue(Object.ReferenceEquals(project, property.Project));
        }

        /// <summary>
        /// Property with nothing to expand
        /// </summary>
        [MSBuildTestMethod]
        public void NoExpansion()
        {
            string content = @"
                    <Project>
                        <PropertyGroup>
                            <p>v1</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectProperty property = GetFirstProperty(content);

            Assert.IsNotNull(property.Xml);
            Assert.AreEqual("p", property.Name);
            Assert.AreEqual("v1", property.EvaluatedValue);
            Assert.AreEqual("v1", property.UnevaluatedValue);
        }

        /// <summary>
        /// Embedded property
        /// </summary>
        [MSBuildTestMethod]
        public void ExpandProperty()
        {
            string content = @"
                    <Project>
                        <PropertyGroup>
                            <o>v1</o>
                            <p>$(o)</p>
                        </PropertyGroup>
                    </Project>
                ";

            ProjectProperty property = GetFirstProperty(content);

            Assert.IsNotNull(property.Xml);
            Assert.AreEqual("p", property.Name);
            Assert.AreEqual("v1", property.EvaluatedValue);
            Assert.AreEqual("$(o)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        [MSBuildTestMethod]
        public void SetValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v2";

            Assert.AreEqual("v2", property.EvaluatedValue);
            Assert.AreEqual("v2", property.UnevaluatedValue);
            Assert.IsTrue(project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        [MSBuildTestMethod]
        public void SetValue_Escaped()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v%282%29";

            Assert.AreEqual("v(2)", property.EvaluatedValue);
            Assert.AreEqual("v%282%29", property.UnevaluatedValue);
            Assert.IsTrue(project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property to the same value.
        /// This should not dirty the project.
        /// </summary>
        [MSBuildTestMethod]
        public void SetValueSameValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v1";

            Assert.IsFalse(project.IsDirty);
        }

        /// <summary>
        /// Attempt to set the value of a built-in property
        /// </summary>
        [MSBuildTestMethod]
        public void InvalidSetValueBuiltInProperty()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = new Project();
                ProjectProperty property = project.GetProperty("MSBuildProjectDirectory");

                property.UnevaluatedValue = "v";
            });
        }
        /// <summary>
        /// Set the value of a property originating in the environment.
        /// Should work even though there is no XML behind it.
        /// Also, should persist.
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void SetValueEnvironmentProperty()
        {
            Project project = new Project();
            string varName = NativeMethodsShared.IsWindows ? "username" : "USER";
            ProjectProperty property = project.GetProperty(varName);

            property.UnevaluatedValue = "v";

            Assert.AreEqual("v", property.EvaluatedValue);
            Assert.AreEqual("v", property.UnevaluatedValue);

            project.ReevaluateIfNecessary();

            property = project.GetProperty(varName);
            Assert.AreEqual("v", property.UnevaluatedValue);
        }

        /// <summary>
        /// Test IsEnvironmentVariable
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void IsEnvironmentVariable()
        {
            Project project = new Project();
            string varName = NativeMethodsShared.IsWindows ? "username" : "USER";

            Assert.IsTrue(project.GetProperty(varName).IsEnvironmentProperty);
            Assert.IsFalse(project.GetProperty(varName).IsGlobalProperty);
            Assert.IsFalse(project.GetProperty(varName).IsReservedProperty);
            Assert.IsFalse(project.GetProperty(varName).IsImported);
        }

        /// <summary>
        /// Test IsGlobalProperty
        /// </summary>
        [MSBuildTestMethod]
        public void IsGlobalProperty()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["g"] = String.Empty;
            Project project = new Project(globalProperties, null, ProjectCollection.GlobalProjectCollection);

            Assert.IsFalse(project.GetProperty("g").IsEnvironmentProperty);
            Assert.IsTrue(project.GetProperty("g").IsGlobalProperty);
            Assert.IsFalse(project.GetProperty("g").IsReservedProperty);
            Assert.IsFalse(project.GetProperty("g").IsImported);
        }

        /// <summary>
        /// Test IsReservedProperty
        /// </summary>
        [MSBuildTestMethod]
        public void IsReservedProperty()
        {
            Project project = new Project();
            project.FullPath = @"c:\x";
            project.ReevaluateIfNecessary();

            Assert.IsFalse(project.GetProperty("MSBuildProjectFile").IsEnvironmentProperty);
            Assert.IsFalse(project.GetProperty("MSBuildProjectFile").IsGlobalProperty);
            Assert.IsTrue(project.GetProperty("MSBuildProjectFile").IsReservedProperty);
            Assert.IsFalse(project.GetProperty("MSBuildProjectFile").IsImported);
        }

        /// <summary>
        /// Verify properties are expanded in new property values
        /// </summary>
        [MSBuildTestMethod]
        public void SetPropertyWithPropertyExpression()
        {
            Project project = new Project();
            project.SetProperty("p0", "v0");
            ProjectProperty property = project.SetProperty("p1", "v1");
            property.UnevaluatedValue = "$(p0)";

            Assert.AreEqual("v0", project.GetPropertyValue("p1"));
            Assert.AreEqual("v0", property.EvaluatedValue);
            Assert.AreEqual("$(p0)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Verify item expressions are not expanded in new property values.
        /// NOTE: They aren't expanded to "blank". It just seems like that, because
        /// when you output them, item expansion happens after property expansion, and
        /// they may evaluate to blank then. (Unless items do exist at that point.)
        /// </summary>
        [MSBuildTestMethod]
        public void SetPropertyWithItemAndMetadataExpression()
        {
            Project project = new Project();
            project.SetProperty("p0", "v0");
            ProjectProperty property = project.SetProperty("p1", "v1");
            property.UnevaluatedValue = "@(i)-%(m)";

            Assert.AreEqual("@(i)-%(m)", project.GetPropertyValue("p1"));
            Assert.AreEqual("@(i)-%(m)", property.EvaluatedValue);
            Assert.AreEqual("@(i)-%(m)", property.UnevaluatedValue);
        }

        /// <summary>
        /// Attempt to set value on imported property should fail
        /// </summary>
        [MSBuildTestMethod]
        public void SetPropertyImported()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                string file = null;

                try
                {
                    file = FileUtilities.GetTemporaryFileName();
                    Project import = new Project();
                    import.SetProperty("p", "v0");
                    import.Save(file);

                    ProjectRootElement xml = ProjectRootElement.Create();
                    xml.AddImport(file);
                    Project project = new Project(xml);

                    ProjectProperty property = project.GetProperty("p");
                    property.UnevaluatedValue = "v1";
                }
                finally
                {
                    File.Delete(file);
                }
            });
        }
        /// <summary>
        /// Get the property named "p" in the project provided
        /// </summary>
        private static ProjectProperty GetFirstProperty(string content)
        {
            using ProjectRootElementFromString projectRootElementFromString = new(content);
            ProjectRootElement projectXml = projectRootElementFromString.Project;

            Project project = new Project(projectXml);
            ProjectProperty property = project.GetProperty("p");

            return property;
        }
    }
}
