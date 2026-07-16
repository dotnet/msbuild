// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance public members
    /// </summary>
    [TestClass]
    public class ProjectPropertyInstance_Tests
    {
        /// <summary>
        /// Get name and value
        /// </summary>
        [MSBuildTestMethod]
        public void Accessors()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            Assert.AreEqual("p", property.Name);
            Assert.AreEqual("v1", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [MSBuildTestMethod]
        public void SetValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v2";
            Assert.AreEqual("v2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [MSBuildTestMethod]
        public void SetValue_Escaped()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v!2";
            Assert.AreEqual("v!2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set empty value
        /// </summary>
        [MSBuildTestMethod]
        public void SetEmptyValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = String.Empty;
            Assert.AreEqual(String.Empty, property.EvaluatedValue);
        }

        /// <summary>
        /// Set invalid null value
        /// </summary>
        [MSBuildTestMethod]
        public void SetInvalidNullValue()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                ProjectPropertyInstance property = GetPropertyInstance();
                property.EvaluatedValue = null;
            });
        }
        /// <summary>
        /// Immutable getter
        /// </summary>
        [MSBuildTestMethod]
        public void ImmutableGetterFalse()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            Assert.IsFalse(property.IsImmutable);
        }

        /// <summary>
        /// Immutable getter true
        /// </summary>
        [MSBuildTestMethod]
        public void ImmutableGetterTrue()
        {
            var project = new Project();
            project.SetProperty("p", "v1");
            var snapshot = project.CreateProjectInstance(ProjectInstanceSettings.Immutable);
            var property = snapshot.GetProperty("p");
            Assert.IsTrue(property.IsImmutable);
        }

        /// <summary>
        /// Get a ProjectPropertyInstance
        /// </summary>
        private static ProjectPropertyInstance GetPropertyInstance()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectPropertyInstance property = projectInstance.SetProperty("p", "v1");

            return property;
        }
    }
}
