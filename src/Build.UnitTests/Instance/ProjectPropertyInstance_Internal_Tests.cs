// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using System;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectPropertyInstance internal members
    /// </summary>
    public class ProjectPropertyInstance_Internal_Tests
    {
        /// <summary>
        /// Cloning
        /// </summary>
        [Fact]
        public void DeepClone()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            ProjectPropertyInstance clone = property.DeepClone();

            Assert.Equal(false, Object.ReferenceEquals(property, clone));
            Assert.Equal("p", clone.Name);
            Assert.Equal("v1", clone.EvaluatedValue);
        }

        /// <summary>
        /// Serialization test
        /// </summary>
        [Fact]
        public void Serialization()
        {
            ProjectPropertyInstance property = GetPropertyInstance();

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.Equal(property.Name, deserializedProperty.Name);
            Assert.Equal(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [Fact]
        public void ProjectPropertyInstanceSerializationTest_Mutable()
        {
            var property = ProjectPropertyInstance.Create("p", "v", false /*mutable*/);
            Assert.Equal(false, property.IsImmutable);

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.Equal(property.Name, deserializedProperty.Name);
            Assert.Equal(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
            Assert.Equal(property.IsImmutable, deserializedProperty.IsImmutable);
            Assert.Equal(typeof(ProjectPropertyInstance), property.GetType());
        }

        /// <summary>
        /// Tests serialization.
        /// </summary>
        [Fact]
        public void ProjectPropertyInstanceSerializationTest_Immutable()
        {
            var property = ProjectPropertyInstance.Create("p", "v", mayBeReserved: true, isImmutable: true);
            Assert.Equal(true, property.IsImmutable);

            TranslationHelpers.GetWriteTranslator().Translate(ref property, ProjectPropertyInstance.FactoryForDeserialization);
            ProjectPropertyInstance deserializedProperty = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedProperty, ProjectPropertyInstance.FactoryForDeserialization);

            Assert.Equal(property.Name, deserializedProperty.Name);
            Assert.Equal(property.EvaluatedValue, deserializedProperty.EvaluatedValue);
            Assert.Equal(property.IsImmutable, deserializedProperty.IsImmutable);
            Assert.Equal("Microsoft.Build.Execution.ProjectPropertyInstance+ProjectPropertyInstanceImmutable", property.GetType().ToString());
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
