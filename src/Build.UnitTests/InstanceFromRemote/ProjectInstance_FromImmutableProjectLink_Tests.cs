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

namespace Microsoft.Build.Engine.UnitTests.InstanceFromRemote
{
    [TestClass]
    public class ProjectInstance_FromImmutableProjectLink_Tests
    {
        /// <summary>
        /// Ensures that a ProjectInstance can be created without accessing lazy properties from an immutable project link.
        /// </summary>
        [MSBuildTestMethod]
        public void ProjectInstanceAccessMinimalState()
        {
            var projectLink = new FakeProjectLink(@"Q:\FakeFolder\Project\Project.proj");
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.IsNotNull(instance);
        }

        /// <summary>
        /// GetPropertyValue will retrive values from project link without accessing lazy properties.
        /// </summary>
        [MSBuildTestMethod]
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
            Assert.IsNotNull(instance);

            // Verify that the properties are accessible
            foreach (var kvp in propertyValues)
            {
                string value = instance.GetPropertyValue(kvp.Key);
                Assert.IsTrue(value == kvp.Value,
                    $"Property '{kvp.Key}' with value '{kvp.Value}' was not found in the ProjectInstance.");
            }
        }

        [MSBuildTestMethod]
        public void ProjectInstanceAccessProjectItems()
        {
            var items = new Dictionary<string, ProjectItemLink[]>(StringComparer.OrdinalIgnoreCase);
            var projectLink = new FakeProjectLink(
                @"Q:\FakeFolder\Project\Project.proj",
                itemDefinitions: new EmptyItemTypeDefinitionDictionary(),
                items: new FakeProjectItemDictionary(items));
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.IsNotNull(instance);

            items.Add("Compile", new[]
            {
                new FakeProjectItemLink(project, "Compile", "File1.cs", @"Q:\FakeFolder\Project\Project.proj", new Dictionary<string, string> { { "Metadata1", "Value1" } }),
                new FakeProjectItemLink(project, "Compile", "File2.cs", @"Q:\FakeFolder\Project\a.props", new Dictionary<string, string> { { "Metadata2", "Value2" } })
            });

            var compileItems = instance.GetItems("Compile").ToList();
            Assert.AreEqual(2, compileItems.Count);

            var item1 = compileItems[0];

            Assert.AreEqual("File1.cs", item1.EvaluatedInclude);
            Assert.AreEqual("Value1", item1.GetMetadataValue("Metadata1"));
            Assert.AreEqual(string.Empty, item1.GetMetadataValue("Metadata2"));
            Assert.AreEqual("Compile", item1.ItemType);

            var item2 = compileItems[1];
            Assert.AreEqual("File2.cs", item2.EvaluatedInclude);
            Assert.AreEqual("Value2", item2.GetMetadataValue("Metadata2"));
            Assert.AreEqual(string.Empty, item2.GetMetadataValue("Metadata1"));
            Assert.AreEqual("Compile", item2.ItemType);
        }

        [MSBuildTestMethod]
        public void ProjectInstanceAccessProjectItemThroughLookup()
        {
            var items = new Dictionary<string, ProjectItemLink[]>(StringComparer.OrdinalIgnoreCase);
            var projectLink = new FakeProjectLinkWithQuickItemLookUp(
                @"File1.cs",
                items,
                itemDefinitions: new EmptyItemTypeDefinitionDictionary());
            var project = new Project(ProjectCollection.GlobalProjectCollection, projectLink);
            ProjectInstance instance = ProjectInstance.FromImmutableProjectSource(project, ProjectInstanceSettings.ImmutableWithFastItemLookup);
            Assert.IsNotNull(instance);

            items.Add("File1.cs", new[]
            {
                new FakeProjectItemLink(project, "Compile", "File1.cs", @"Q:\FakeFolder\Project\Project.proj", new Dictionary<string, string> { { "Metadata1", "Value1" } }),
            });

            var compileItems = instance.GetItemsByItemTypeAndEvaluatedInclude("Compile", "File1.cs").ToList();
            Assert.AreEqual(1, compileItems.Count);

            var item1 = compileItems[0];

            Assert.AreEqual("File1.cs", item1.EvaluatedInclude);
            Assert.AreEqual("Value1", item1.GetMetadataValue("Metadata1"));
            Assert.AreEqual(string.Empty, item1.GetMetadataValue("Metadata2"));
            Assert.AreEqual("Compile", item1.ItemType);
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
