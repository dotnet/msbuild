// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for ProjectPropertyInstance internal members</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.UnitTests.BackEnd;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance internal members
    /// </summary>
    [TestClass]
    public class ProjectPropertyInstance_Internal_Tests
    {
        /// <summary>
        /// Cloning
        /// </summary>
        [TestMethod]
        public void DeepClone()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            ProjectPropertyInstance clone = property.DeepClone();

            Assert.AreEqual(false, Object.ReferenceEquals(property, clone));
            Assert.AreEqual("p", clone.Name);
            Assert.AreEqual("v1", clone.EvaluatedValue);
        }

        /// <summary>
        /// Serialization test
        /// </summary>
        [TestMethod]
        public void Serialization()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.AreEqual(property.Name, deserializedProperty.Name);
            Assert.AreEqual(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [TestMethod]
        public void ProjectPropertyInstanceSerializationTest_Mutable()
        {
            var property = ProjectPropertyInstance.Create("p", "v", false /*mutable*/);
            Assert.AreEqual(false, property.IsImmutable);

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.AreEqual(property.Name, deserializedProperty.Name);
            Assert.AreEqual(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
            Assert.AreEqual(property.IsImmutable, deserializedProperty.IsImmutable);
            Assert.AreEqual(typeof(ProjectPropertyInstance), property.GetType());
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [TestMethod]
        public void ProjectPropertyInstanceSerializationTest_Immutable()
        {
            var property = ProjectPropertyInstance.Create("p", "v", mayBeReserved: true, isImmutable: true);
            Assert.AreEqual(true, property.IsImmutable);

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.AreEqual(property.Name, deserializedProperty.Name);
            Assert.AreEqual(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
            Assert.AreEqual(property.IsImmutable, deserializedProperty.IsImmutable);
            Assert.AreEqual("Microsoft.Build.Execution.ProjectPropertyInstance+ProjectPropertyInstanceImmutable", property.GetType().ToString());
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
