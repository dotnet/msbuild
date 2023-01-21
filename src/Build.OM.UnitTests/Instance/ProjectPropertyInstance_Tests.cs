// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance public members
    /// </summary>
    public class ProjectPropertyInstance_Tests
    {
        /// <summary>
        /// Get name and value
        /// </summary>
        [Fact]
        public void Accessors()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            Assert.Equal("p", property.Name);
            Assert.Equal("v1", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [Fact]
        public void SetValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v2";
            Assert.Equal("v2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [Fact]
        public void SetValue_Escaped()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v!2";
            Assert.Equal("v!2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set empty value
        /// </summary>
        [Fact]
        public void SetEmptyValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = String.Empty;
            Assert.Equal(String.Empty, property.EvaluatedValue);
        }

        /// <summary>
        /// Set invalid null value
        /// </summary>
        [Fact]
        public void SetInvalidNullValue()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ProjectPropertyInstance property = GetPropertyInstance();
                property.EvaluatedValue = null;
            });
        }
        /// <summary>
        /// Immutable getter
        /// </summary>
        [Fact]
        public void ImmutableGetterFalse()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            Assert.False(property.IsImmutable);
        }

        /// <summary>
        /// Immutable getter true
        /// </summary>
        [Fact]
        public void ImmutableGetterTrue()
        {
            var project = new Project();
            project.SetProperty("p", "v1");
            var snapshot = project.CreateProjectInstance(ProjectInstanceSettings.Immutable);
            var property = snapshot.GetProperty("p");
            Assert.True(property.IsImmutable);
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
