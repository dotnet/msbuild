// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

#nullable disable

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

            Assert.False(Object.ReferenceEquals(property, clone));
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
            Assert.False(property.IsImmutable);

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
            Assert.True(property.IsImmutable);

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
