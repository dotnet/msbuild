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
    /// Tests for ProjectMetadataInstance internal members
    /// </summary>
    public class ProjectMetadataInstance_Internal_Tests
    {
        /// <summary>
        /// Cloning
        /// </summary>
        [Fact]
        public void DeepClone()
        {
            ProjectMetadataInstance metadata = GetMetadataInstance();

            ProjectMetadataInstance clone = metadata.DeepClone();

            Assert.False(Object.ReferenceEquals(metadata, clone));
            Assert.Equal("m", clone.Name);
            Assert.Equal("m1", clone.EvaluatedValue);
        }

        /// <summary>
        /// Tests serialization
        /// </summary>
        [Fact]
        public void Serialization()
        {
            ProjectMetadataInstance metadata = new ProjectMetadataInstance("m1", "v1", false);

            TranslationHelpers.GetWriteTranslator().Translate(ref metadata, ProjectMetadataInstance.FactoryForDeserialization);
            ProjectMetadataInstance deserializedMetadata = null;
            TranslationHelpers.GetReadTranslator().Translate(ref deserializedMetadata, ProjectMetadataInstance.FactoryForDeserialization);

            Assert.Equal(metadata.Name, deserializedMetadata.Name);
            Assert.Equal(metadata.EvaluatedValue, deserializedMetadata.EvaluatedValue);
        }

        /// <summary>
        /// Get a single metadata instance
        /// </summary>
        private static ProjectMetadataInstance GetMetadataInstance()
        {
            Project project = new Project();
            ProjectInstance projectInstance = project.CreateProjectInstance();
            ProjectItemInstance item = projectInstance.AddItem("i", "i1");
            ProjectMetadataInstance metadata = item.SetMetadata("m", "m1");
            return metadata;
        }
    }
}
