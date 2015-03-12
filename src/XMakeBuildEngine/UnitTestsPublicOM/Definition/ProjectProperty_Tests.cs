//-----------------------------------------------------------------------
// <copyright file="ProjectProperty_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for ProjectProperty</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        [TestMethod]
        public void ProjectGetter()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v");

            Assert.AreEqual(true, Object.ReferenceEquals(project, property.Project));
        }

        /// <summary>
        /// Property with nothing to expand
        /// </summary>
        [TestMethod]
        public void NoExpansion()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
        [TestMethod]
        public void ExpandProperty()
        {
            string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
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
        [TestMethod]
        public void SetValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v2";
            
            Assert.AreEqual("v2", property.EvaluatedValue);
            Assert.AreEqual("v2", property.UnevaluatedValue);
            Assert.AreEqual(true, project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property
        /// </summary>
        [TestMethod]
        public void SetValue_Escaped()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v%282%29";

            Assert.AreEqual("v(2)", property.EvaluatedValue);
            Assert.AreEqual("v%282%29", property.UnevaluatedValue);
            Assert.AreEqual(true, project.IsDirty);
        }

        /// <summary>
        /// Set the value of a property to the same value.
        /// This should not dirty the project.
        /// </summary>
        [TestMethod]
        public void SetValueSameValue()
        {
            Project project = new Project();
            ProjectProperty property = project.SetProperty("p", "v1");
            project.ReevaluateIfNecessary();

            property.UnevaluatedValue = "v1";

            Assert.AreEqual(false, project.IsDirty);
        }

        /// <summary>
        /// Attempt to set the value of a built-in property
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void InvalidSetValueBuiltInProperty()
        {
            Project project = new Project();
            ProjectProperty property = project.GetProperty("MSBuildProjectDirectory");

            property.UnevaluatedValue = "v";
        }

        /// <summary>
        /// Set the value of a property originating in the environment.
        /// Should work even though there is no XML behind it.
        /// Also, should persist.
        /// </summary>
        [TestMethod]
        public void SetValueEnvironmentProperty()
        {
            Project project = new Project();
            ProjectProperty property = project.GetProperty("Username");

            property.UnevaluatedValue = "v";

            Assert.AreEqual("v", property.EvaluatedValue);
            Assert.AreEqual("v", property.UnevaluatedValue);

            project.ReevaluateIfNecessary();

            property = project.GetProperty("Username");
            Assert.AreEqual("v", property.UnevaluatedValue);
        }

        /// <summary>
        /// Test IsEnvironmentVariable
        /// </summary>
        [TestMethod]
        public void IsEnvironmentVariable()
        {
            Project project = new Project();

            Assert.AreEqual(true, project.GetProperty("username").IsEnvironmentProperty);
            Assert.AreEqual(false, project.GetProperty("username").IsGlobalProperty);
            Assert.AreEqual(false, project.GetProperty("username").IsReservedProperty);
            Assert.AreEqual(false, project.GetProperty("username").IsImported);
        }

        /// <summary>
        /// Test IsGlobalProperty
        /// </summary>
        [TestMethod]
        public void IsGlobalProperty()
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties["g"] = String.Empty;
            Project project = new Project(globalProperties, null, ProjectCollection.GlobalProjectCollection);

            Assert.AreEqual(false, project.GetProperty("g").IsEnvironmentProperty);
            Assert.AreEqual(true, project.GetProperty("g").IsGlobalProperty);
            Assert.AreEqual(false, project.GetProperty("g").IsReservedProperty);
            Assert.AreEqual(false, project.GetProperty("g").IsImported);
        }

        /// <summary>
        /// Test IsReservedProperty
        /// </summary>
        [TestMethod]
        public void IsReservedProperty()
        {
            Project project = new Project();
            project.FullPath = @"c:\x";
            project.ReevaluateIfNecessary();

            Assert.AreEqual(false, project.GetProperty("MSBuildProjectFile").IsEnvironmentProperty);
            Assert.AreEqual(false, project.GetProperty("MSBuildProjectFile").IsGlobalProperty);
            Assert.AreEqual(true, project.GetProperty("MSBuildProjectFile").IsReservedProperty);
            Assert.AreEqual(false, project.GetProperty("MSBuildProjectFile").IsImported);
        }

        /// <summary>
        /// Verify properties are expanded in new property values
        /// </summary>
        [TestMethod]
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
        [TestMethod]
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
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SetPropertyImported()
        {
            string file = null;

            try
            {
                file = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile();
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
        }

        /// <summary>
        /// Get the property named "p" in the project provided
        /// </summary>
        private static ProjectProperty GetFirstProperty(string content)
        {
            ProjectRootElement projectXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(projectXml);
            ProjectProperty property = project.GetProperty("p");

            return property;
        }
    }
}
