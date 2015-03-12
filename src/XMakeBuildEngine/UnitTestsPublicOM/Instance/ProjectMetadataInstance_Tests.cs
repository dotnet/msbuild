//-----------------------------------------------------------------------
// <copyright file="ProjectMetadataInstance_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for ProjectMetadataInstance public members</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Instance
{
    /// <summary>
    /// Tests for ProjectMetadataInstance public members
    /// </summary>
    [TestClass]
    public class ProjectMetadataInstance_Tests
    {
        /// <summary>
        /// Get name and value
        /// </summary>
        [TestMethod]
        public void Accessors()
        {
            ProjectMetadataInstance metadata = GetMetadataInstance();

            Assert.AreEqual("m", metadata.Name);
            Assert.AreEqual("m1", metadata.EvaluatedValue);
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
