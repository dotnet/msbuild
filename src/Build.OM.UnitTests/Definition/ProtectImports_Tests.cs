// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for protecting imported files while editing
    /// </summary>
    public class ProtectImports_Tests : IDisposable
    {
        #region Constants

        /// <summary>
        /// Imported metadata name
        /// </summary>
        private const string ImportedMetadataName = "ImportedMetadataName";

        /// <summary>
        /// Imported metadata value
        /// </summary>
        private const string ImportedMetadataValue = "ImportedMetadataValue";

        /// <summary>
        /// Item name to use
        /// </summary>
        private const string ItemName = "ItemName";

        /// <summary>
        /// Item type to use
        /// </summary>
        private const string ItemType = "ItemType";

        /// <summary>
        /// New value
        /// </summary>
        private const string NewValue = "NewValue";

        /// <summary>
        /// It's non-overridable just in the sense that the tests aren't providing new values to it; nothing else implied
        /// </summary>
        private const string NonOverridableMetadataName = "NonOverridableMetadataName";

        /// <summary>
        /// Overridable metadata name
        /// </summary>
        private const string OverridableMetadataName = "OverridableMetadataName";

        /// <summary>
        /// Project metadata name
        /// </summary>
        private const string ProjectMetadataName = "ProjectMetadataName";

        /// <summary>
        /// Project metadata value
        /// </summary>
        private const string ProjectMetadataValue = "ProjectMetadataValue";

        /// <summary>
        /// Same item type
        /// </summary>
        private const string SameItemType = "SameItemType";

        /// <summary>
        /// Same item value in project
        /// </summary>
        private const string SameItemValueInProject = "SameItemValueInProject";

        /// <summary>
        /// Same property name
        /// </summary>
        private const string PropertyName = "ImportedProperty";

        #endregion

        /// <summary>
        /// Import filename
        /// </summary>
        private string _importFilename;

        #region Test lifetime

        /// <summary>
        /// Configures the overall test.
        /// </summary>
        public ProtectImports_Tests()
        {
            string importContents =
                @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <$propertyName>OldPropertyValue</$propertyName>
                    </PropertyGroup>
                    <ItemDefinitionGroup>
                        <$itemType>
                            <$overridableMetadataName>ImportValue</$overridableMetadataName>
                            <$nonOverridableMetadataName>ImportValue</$nonOverridableMetadataName>
                        </$itemType>
                    </ItemDefinitionGroup>
                    <ItemGroup>
                        <$itemType Include=""$itemName"">
                            <$importedMetadataName>$importedMetadataValue</$importedMetadataName>
                        </$itemType>
                    </ItemGroup>
                </Project>";

            importContents = Expand(importContents);
            _importFilename = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile() + ".targets";
            File.WriteAllText(_importFilename, importContents);
        }

        /// <summary>
        /// Undoes the test configuration.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(_importFilename))
            {
                File.Delete(_importFilename);
            }
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests against edits into imported properties through the property itself.
        /// </summary>
        [Fact]
        public void PropertySetViaProperty()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectProperty property = GetProperty(project);

                // This should throw
                property.UnevaluatedValue = NewValue;
            }
           );
        }
        /// <summary>
        /// Tests against edits into imported properties through the project.
        /// Instead of editing the existing property, because it originated
        /// in an imported file, it should create a new one in the main project.
        /// </summary>
        [Fact]
        public void PropertySetViaProject()
        {
            Project project = GetProject();
            ProjectProperty property = GetProperty(project);

            project.SetProperty(PropertyName, NewValue);

            Assert.Equal(NewValue, project.GetPropertyValue(PropertyName));
        }

        /// <summary>
        /// Tests against edits into imported properties through the property itself.
        /// </summary>
        [Fact]
        public void PropertyRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectProperty property = GetProperty(project);

                // This should throw
                project.RemoveProperty(property);
            }
           );
        }
        #endregion

        #region Item Tests

        /// <summary>
        /// Tests imported item type change.
        /// </summary>
        [Fact]
        public void ItemImportedChangeType()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.ItemType = "NewItemType";
            }
           );
        }
        /// <summary>
        /// Tests imported item renaming.
        /// </summary>
        [Fact]
        public void ItemImportedRename()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.Rename("NewItemName");
            }
           );
        }
        /// <summary>
        /// Tests imported item SetUnevaluatedValue.
        /// </summary>
        [Fact]
        public void ItemImportedSetUnevaluatedValue()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.UnevaluatedInclude = "NewItemName";
            }
           );
        }
        /// <summary>
        /// Tests imported item removal.
        /// </summary>
        [Fact]
        public void ItemImportedRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                project.RemoveItem(item);
            }
           );
        }
        /// <summary>
        /// Tests project item type change.
        /// </summary>
        [Fact]
        public void ItemProjectChangeType()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.ItemType = NewValue;
            Assert.Equal(1, project.GetItems(NewValue).Count()); // "Item in project didn't change name"
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item renaming.
        /// </summary>
        [Fact]
        public void ItemProjectRename()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.Rename(NewValue);
            Assert.Equal(NewValue, item.EvaluatedInclude); // "Item in project didn't change name."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item SetUnevaluatedValue.
        /// </summary>
        [Fact]
        public void ItemProjectSetUnevaluatedValue()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.UnevaluatedInclude = NewValue;
            Assert.Equal(NewValue, item.EvaluatedInclude); // "Item in project didn't change name."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item removal.
        /// </summary>
        [Fact]
        public void ItemProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            project.RemoveItem(item);
            Assert.Equal(1, project.GetItems(ItemType).Count()); // "Item in project wasn't removed."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        #endregion

        #region Metadata Tests

        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [Fact]
        public void MetadataImportSetViaProject()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.SetMetadataValue(ImportedMetadataName, "NewImportedMetadataValue");
            }
           );
        }
        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void MetadataImportAdd()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.SetMetadataValue("NewMetadata", "NewImportedMetadataValue");
            }
           );
        }
        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void MetadataImportSetViaMetadata()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectMetadata metadata = GetImportedMetadata(project);

                // This should throw
                metadata.UnevaluatedValue = NewValue;
            }
           );
        }
        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [Fact]
        public void MetadataImportRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.RemoveMetadata(ImportedMetadataName);
            }
           );
        }
        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [Fact]
        public void MetadataProjectSetViaItem()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(ProjectMetadataName, NewValue);
            Assert.Equal(NewValue, item.GetMetadataValue(ProjectMetadataName)); // "Metadata not saved correctly in project."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void MetadataProjectAdd()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            string newName = "NewMetadata";
            item.SetMetadataValue(newName, NewValue);
            Assert.Equal(NewValue, item.GetMetadataValue(newName)); // "Metadata not saved correctly in project."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void MetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetProjectMetadata(project);

            string newValue = "NewProjectMetadataValue";
            metadata.UnevaluatedValue = newValue;

            Assert.Equal(newValue, metadata.EvaluatedValue);
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [Fact]
        public void MetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(ProjectMetadataName);
            Assert.False(item.HasMetadata(ProjectMetadataName)); // "Metadata was not removed from project."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        #endregion

        #region Metadata in Item Definition Tests

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void DefinitionMetadataImportSetViaMetadata()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectMetadata metadata = GetNonOverridableMetadata(project);

                // This should throw
                metadata.UnevaluatedValue = NewValue;
            }
           );
        }
        /// <summary>
        /// Tests removing metadata in imported item definition.
        /// </summary>
        [Fact]
        public void DefinitionMetadataImportRemove()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetProjectItem(project);

                // This should throw
                item.RemoveMetadata(NonOverridableMetadataName);
            }
           );
        }
        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [Fact]
        public void DefinitionMetadataProjectSetViaProject()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(OverridableMetadataName, NewValue);
            Assert.Equal(NewValue, item.GetMetadataValue(OverridableMetadataName)); // "Metadata not set correctly in project."
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [Fact]
        public void DefinitionMetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetOverridableMetadata(project);

            metadata.UnevaluatedValue = NewValue;
            Assert.Equal(NewValue, metadata.EvaluatedValue);
            Assert.True(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [Fact]
        public void DefinitionMetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(OverridableMetadataName);

            ProjectMetadata metadata = item.GetMetadata(OverridableMetadataName);
            Assert.NotNull(metadata); // "Imported metadata not found after the project's one was removed."
            Assert.True(metadata.IsImported); // "IsImported property is not set."
        }

        #endregion

        #region Test helpers

        /// <summary>
        /// Expands variables in the string contents.
        /// </summary>
        /// <param name="original">String to be expanded.</param>
        /// <returns>Expanded string.</returns>
        private string Expand(string original)
        {
            string expanded = original.Replace("$importFilename", _importFilename);
            expanded = expanded.Replace("$importedMetadataName", ImportedMetadataName);
            expanded = expanded.Replace("$importedMetadataValue", ImportedMetadataValue);
            expanded = expanded.Replace("$itemName", ItemName);
            expanded = expanded.Replace("$itemType", ItemType);
            expanded = expanded.Replace("$projectMetadataName", ProjectMetadataName);
            expanded = expanded.Replace("$overridableMetadataName", OverridableMetadataName);
            expanded = expanded.Replace("$nonOverridableMetadataName", NonOverridableMetadataName);
            expanded = expanded.Replace("$projectMetadataValue", ProjectMetadataValue);
            expanded = expanded.Replace("$propertyName", PropertyName);

            return expanded;
        }

        /// <summary>
        /// Gets the test item from the import.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The item.</returns>
        private ProjectItem GetImportedItem(Project project)
        {
            IEnumerable<ProjectItem> items = project.GetItems(ItemType).Where(pi => pi.IsImported);
            Assert.Equal(1, items.Count()); // "Wrong number of items in the import."

            ProjectItem item = items.First();
            Assert.Equal(_importFilename, item.Xml.ContainingProject.FullPath); // "Item was not found in the imported project."

            return item;
        }

        /// <summary>
        /// Gets the test metadata from the import.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The metadata.</returns>
        private ProjectMetadata GetImportedMetadata(Project project)
        {
            ProjectItem item = GetImportedItem(project);
            IEnumerable<ProjectMetadata> metadatum = item.Metadata.Where(m => m.Name == ImportedMetadataName);
            Assert.Equal(1, metadatum.Count()); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.True(metadata.IsImported); // "IsImport property is not set."

            return metadata;
        }

        /// <summary>
        /// Gets the test templetized metadata from the import.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The metadata.</returns>
        private ProjectMetadata GetNonOverridableMetadata(Project project)
        {
            ProjectItem item = GetProjectItem(project);
            IEnumerable<ProjectMetadata> metadatum = item.Metadata.Where(m => m.Name == NonOverridableMetadataName);
            Assert.Equal(1, metadatum.Count()); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.True(metadata.IsImported); // "IsImport property is not set."

            return metadata;
        }

        /// <summary>
        /// Gets the test templetized metadata from the project.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The metadata.</returns>
        private ProjectMetadata GetOverridableMetadata(Project project)
        {
            ProjectItem item = GetProjectItem(project);
            IEnumerable<ProjectMetadata> metadatum = item.Metadata.Where(m => m.Name == OverridableMetadataName);
            Assert.Equal(1, metadatum.Count()); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.False(metadata.IsImported); // "IsImport property is set."

            return metadata;
        }

        /// <summary>
        /// Creates a new project from expanding the template contents.
        /// </summary>
        /// <returns>The project instance.</returns>
        private Project GetProject()
        {
            string projectContents =
                @"<Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003'>
                    <PropertyGroup>
                        <$propertyName>OldPropertyValueInProject</$propertyName>
                    </PropertyGroup>
                    <ItemGroup>
                        <$itemType Include=""$itemName"">
                            <$projectMetadataName>projectValue</$projectMetadataName>
                            <$overridableMetadataName>ProjectValue</$overridableMetadataName>
                        </$itemType>
                    </ItemGroup>
                    <Import Project=""$importFilename""/>
                </Project>";

            projectContents = Expand(projectContents);
            Project project = new Project(XmlReader.Create(new StringReader(projectContents)));
            return project;
        }

        /// <summary>
        /// Gets the test property.
        /// </summary>
        /// <param name="project">The test project.</param>
        /// <returns>The test property.</returns>
        private ProjectProperty GetProperty(Project project)
        {
            ProjectProperty property = project.GetProperty(PropertyName);
            Assert.Equal(_importFilename, property.Xml.ContainingProject.FullPath); // "Property was not found in the imported project."
            Assert.True(property.IsImported); // "IsImported property was not set."
            return property;
        }

        /// <summary>
        /// Gets the test item from the project.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The item.</returns>
        private ProjectItem GetProjectItem(Project project)
        {
            IEnumerable<ProjectItem> items = project.GetItems(ItemType).Where(pi => !pi.IsImported);
            Assert.Equal(1, items.Count()); // "Wrong number of items in the project."

            ProjectItem item = items.First();
            Assert.Equal(null, item.Xml.ContainingProject.FullPath); // "Item was not found in the project." // null because XML is in-memory

            return item;
        }

        /// <summary>
        /// Gets the test metadata from the project.
        /// </summary>
        /// <param name="project">The project.</param>
        /// <returns>The metadata.</returns>
        private ProjectMetadata GetProjectMetadata(Project project)
        {
            ProjectItem item = GetProjectItem(project);
            IEnumerable<ProjectMetadata> metadatum = item.Metadata.Where(m => m.Name == ProjectMetadataName);
            Assert.Equal(1, metadatum.Count()); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.False(metadata.IsImported); // "IsImport property is set."

            return metadata;
        }

        #endregion
    }
}
