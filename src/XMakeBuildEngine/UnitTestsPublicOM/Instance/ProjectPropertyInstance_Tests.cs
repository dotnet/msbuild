// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="ProjectPropertyInstance_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for ProjectPropertyInstance public members</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance public members
    /// </summary>
    [TestFixture]
    public class ProjectPropertyInstance_Tests
    {
        /// <summary>
        /// Get name and value
        /// </summary>
        [Test]
        public void Accessors()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            Assert.AreEqual("p", property.Name);
            Assert.AreEqual("v1", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [Test]
        public void SetValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v2";
            Assert.AreEqual("v2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set value
        /// </summary>
        [Test]
        public void SetValue_Escaped()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = "v!2";
            Assert.AreEqual("v!2", property.EvaluatedValue);
        }

        /// <summary>
        /// Set empty value
        /// </summary>
        [Test]
        public void SetEmptyValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = String.Empty;
            Assert.AreEqual(String.Empty, property.EvaluatedValue);
        }

        /// <summary>
        /// Set invalid null value
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void SetInvalidNullValue()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            property.EvaluatedValue = null;
        }

        /// <summary>
        /// Immutable getter
        /// </summary>
        [Test]
        public void ImmutableGetterFalse()
        {
            ProjectPropertyInstance property = GetPropertyInstance();
            Assert.AreEqual(false, property.IsImmutable);
        }

        /// <summary>
        /// Immutable getter true
        /// </summary>
        [Test]
        public void ImmutableGetterTrue()
        {
            var project = new Project();
            project.SetProperty("p", "v1");
            var snapshot = project.CreateProjectInstance(ProjectInstanceSettings.Immutable);
            var property = snapshot.GetProperty("p");
            Assert.AreEqual(true, property.IsImmutable);
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
