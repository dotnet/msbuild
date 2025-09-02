// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectMetadataInstance public members
    /// </summary>
    public class ProjectMetadataInstance_Tests
    {
        /// <summary>
        /// Get name and value
        /// </summary>
        [Fact]
        public void Accessors()
        {
            ProjectMetadataInstance metadata = GetMetadataInstance();

            Assert.Equal("m", metadata.Name);
            Assert.Equal("m1", metadata.EvaluatedValue);
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
