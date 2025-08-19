// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    public class ProjectInstance_FromImmutableProjectLink_Tests
    {
        /// <summary>
        /// Ensures that a ProjectInstance can be created without accessing lazy properties from an immutable project link.
        /// </summary>
        [Fact]
        public void ProjectInstanceAccessMinimalState()
        {
            var projectLink = new FakeProjectLink(@"Q:\FakeFolder\Project\Project.proj");
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.NotNull(instance);
        }

        /// <summary>
        /// GetPropertyValue will retrive values from project link without accessing lazy properties.
        /// </summary>
        [Fact]
        public void ProjectInstanceAccessPropertyValues()
        {
            var propertyValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Property1", "Value1" },
                { "Property2", "Value2" }
            };

            var projectLink = new FakeProjectLinkWithPropertyValues(@"Q:\FakeFolder\Project\Project.proj", propertyValues);
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.NotNull(instance);

            // Verify that the properties are accessible
            foreach (var kvp in propertyValues)
            {
                string value = instance.GetPropertyValue(kvp.Key);
                Assert.True(value == kvp.Value,
                    $"Property '{kvp.Key}' with value '{kvp.Value}' was not found in the ProjectInstance.");
            }
        }
    }
}
