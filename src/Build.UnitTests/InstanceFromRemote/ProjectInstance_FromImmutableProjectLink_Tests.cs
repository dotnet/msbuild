// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.ObjectModelRemoting;
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

        [Fact]
        public void ProjectInstanceAccessProjectItems()
        {
            var items = new Dictionary<string, ProjectItemLink[]>(StringComparer.OrdinalIgnoreCase);
            var projectLink = new FakeProjectLink(
                @"Q:\FakeFolder\Project\Project.proj",
                itemDefinitions: new EmptyItemTypeDefinitionDictionary(),
                items: new FakeProjectItemDictionary(items));
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.NotNull(instance);

            items.Add("Compile", new[]
            {
                new FakeProjectItemLink(project, "Compile", "File1.cs", @"Q:\FakeFolder\Project\Project.proj", new Dictionary<string, string> { { "Metadata1", "Value1" } }),
                new FakeProjectItemLink(project, "Compile", "File2.cs", @"Q:\FakeFolder\Project\a.props", new Dictionary<string, string> { { "Metadata2", "Value2" } })
            });

            var compileItems = instance.GetItems("Compile").ToList();
            Assert.Equal(2, compileItems.Count);

            var item1 = compileItems[0];

            Assert.Equal("File1.cs", item1.EvaluatedInclude);
            Assert.Equal("Value1", item1.GetMetadataValue("Metadata1"));
            Assert.Equal(string.Empty, item1.GetMetadataValue("Metadata2"));
            Assert.Equal("Compile", item1.ItemType);

            var item2 = compileItems[1];
            Assert.Equal("File2.cs", item2.EvaluatedInclude);
            Assert.Equal("Value2", item2.GetMetadataValue("Metadata2"));
            Assert.Equal(string.Empty, item2.GetMetadataValue("Metadata1"));
            Assert.Equal("Compile", item2.ItemType);
        }

        [Fact]
        public void ProjectInstanceAccessProjectItemThroughLookup()
        {
            var items = new Dictionary<string, ProjectItemLink[]>(StringComparer.OrdinalIgnoreCase);
            var projectLink = new FakeProjectLinkWithQuickItemLookUp(
                @"File1.cs",
                items,
                itemDefinitions: new EmptyItemTypeDefinitionDictionary());
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.NotNull(instance);

            items.Add("File1.cs", new[]
            {
                new FakeProjectItemLink(project, "Compile", "File1.cs", @"Q:\FakeFolder\Project\Project.proj", new Dictionary<string, string> { { "Metadata1", "Value1" } }),
            });

            var compileItems = instance.GetItemsByItemTypeAndEvaluatedInclude("Compile", "File1.cs").ToList();
            Assert.Equal(1, compileItems.Count);

            var item1 = compileItems[0];

            Assert.Equal("File1.cs", item1.EvaluatedInclude);
            Assert.Equal("Value1", item1.GetMetadataValue("Metadata1"));
            Assert.Equal(string.Empty, item1.GetMetadataValue("Metadata2"));
            Assert.Equal("Compile", item1.ItemType);
        }

        private sealed class EmptyItemTypeDefinitionDictionary : FakeCachedEntityDictionary<ProjectItemDefinition>
        {
            public override bool TryGetValue(string key, out ProjectItemDefinition value)
            {
                value = null!;
                return false;
            }
        }
    }
}
