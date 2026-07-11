// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for protecting imported files while editing
    /// </summary>
    [TestClass]
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
                @"<Project>
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
            _importFilename = FileUtilities.GetTemporaryFileName() + ".targets";
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
        [MSBuildTestMethod]
        public void PropertySetViaProperty()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectProperty property = GetProperty(project);

                // This should throw
                property.UnevaluatedValue = NewValue;
            });
        }
        /// <summary>
        /// Tests against edits into imported properties through the project.
        /// Instead of editing the existing property, because it originated
        /// in an imported file, it should create a new one in the main project.
        /// </summary>
        [MSBuildTestMethod]
        public void PropertySetViaProject()
        {
            Project project = GetProject();
            GetProperty(project);

            project.SetProperty(PropertyName, NewValue);

            Assert.AreEqual(NewValue, project.GetPropertyValue(PropertyName));
        }

        /// <summary>
        /// Tests against edits into imported properties through the property itself.
        /// </summary>
        [MSBuildTestMethod]
        public void PropertyRemove()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectProperty property = GetProperty(project);

                // This should throw
                project.RemoveProperty(property);
            });
        }
        #endregion

        #region Item Tests

        /// <summary>
        /// Tests imported item type change.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemImportedChangeType()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.ItemType = "NewItemType";
            });
        }
        /// <summary>
        /// Tests imported item renaming.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemImportedRename()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.Rename("NewItemName");
            });
        }
        /// <summary>
        /// Tests imported item SetUnevaluatedValue.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemImportedSetUnevaluatedValue()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.UnevaluatedInclude = "NewItemName";
            });
        }
        /// <summary>
        /// Tests imported item removal.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemImportedRemove()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                project.RemoveItem(item);
            });
        }
        /// <summary>
        /// Tests project item type change.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemProjectChangeType()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.ItemType = NewValue;
            Assert.ContainsSingle(project.GetItems(NewValue)); // "Item in project didn't change name"
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item renaming.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemProjectRename()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.Rename(NewValue);
            Assert.AreEqual(NewValue, item.EvaluatedInclude); // "Item in project didn't change name."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item SetUnevaluatedValue.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemProjectSetUnevaluatedValue()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.UnevaluatedInclude = NewValue;
            Assert.AreEqual(NewValue, item.EvaluatedInclude); // "Item in project didn't change name."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests project item removal.
        /// </summary>
        [MSBuildTestMethod]
        public void ItemProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            project.RemoveItem(item);
            Assert.ContainsSingle(project.GetItems(ItemType)); // "Item in project wasn't removed."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        #endregion

        #region Metadata Tests

        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataImportSetViaProject()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.SetMetadataValue(ImportedMetadataName, "NewImportedMetadataValue");
            });
        }
        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataImportAdd()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.SetMetadataValue("NewMetadata", "NewImportedMetadataValue");
            });
        }
        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataImportSetViaMetadata()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectMetadata metadata = GetImportedMetadata(project);

                // This should throw
                metadata.UnevaluatedValue = NewValue;
            });
        }
        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataImportRemove()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetImportedItem(project);

                // This should throw
                item.RemoveMetadata(ImportedMetadataName);
            });
        }
        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataProjectSetViaItem()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(ProjectMetadataName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(ProjectMetadataName)); // "Metadata not saved correctly in project."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataProjectAdd()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            string newName = "NewMetadata";
            item.SetMetadataValue(newName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(newName)); // "Metadata not saved correctly in project."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetProjectMetadata(project);

            string newValue = "NewProjectMetadataValue";
            metadata.UnevaluatedValue = newValue;

            Assert.AreEqual(newValue, metadata.EvaluatedValue);
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void MetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(ProjectMetadataName);
            Assert.IsFalse(item.HasMetadata(ProjectMetadataName)); // "Metadata was not removed from project."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        #endregion

        #region Metadata in Item Definition Tests

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void DefinitionMetadataImportSetViaMetadata()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectMetadata metadata = GetNonOverridableMetadata(project);

                // This should throw
                metadata.UnevaluatedValue = NewValue;
            });
        }
        /// <summary>
        /// Tests removing metadata in imported item definition.
        /// </summary>
        [MSBuildTestMethod]
        public void DefinitionMetadataImportRemove()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                Project project = GetProject();
                ProjectItem item = GetProjectItem(project);

                // This should throw
                item.RemoveMetadata(NonOverridableMetadataName);
            });
        }
        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void DefinitionMetadataProjectSetViaProject()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(OverridableMetadataName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(OverridableMetadataName)); // "Metadata not set correctly in project."
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void DefinitionMetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetOverridableMetadata(project);

            metadata.UnevaluatedValue = NewValue;
            Assert.AreEqual(NewValue, metadata.EvaluatedValue);
            Assert.IsTrue(project.IsDirty); // "Project was not marked dirty."
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [MSBuildTestMethod]
        public void DefinitionMetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(OverridableMetadataName);

            ProjectMetadata metadata = item.GetMetadata(OverridableMetadataName);
            Assert.IsNotNull(metadata); // "Imported metadata not found after the project's one was removed."
            Assert.IsTrue(metadata.IsImported); // "IsImported property is not set."
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
            Assert.ContainsSingle(items); // "Wrong number of items in the import."

            ProjectItem item = items.First();
            Assert.AreEqual(_importFilename, item.Xml.ContainingProject.FullPath); // "Item was not found in the imported project."

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
            Assert.ContainsSingle(metadatum); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.IsTrue(metadata.IsImported); // "IsImport property is not set."

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
            Assert.ContainsSingle(metadatum); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.IsTrue(metadata.IsImported); // "IsImport property is not set."

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
            Assert.ContainsSingle(metadatum); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.IsFalse(metadata.IsImported); // "IsImport property is set."

            return metadata;
        }

        /// <summary>
        /// Creates a new project from expanding the template contents.
        /// </summary>
        /// <returns>The project instance.</returns>
        private Project GetProject()
        {
            string projectContents =
                @"<Project>
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
            using ProjectFromString projectFromString = new(projectContents);
            Project project = projectFromString.Project;

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
            Assert.AreEqual(_importFilename, property.Xml.ContainingProject.FullPath); // "Property was not found in the imported project."
            Assert.IsTrue(property.IsImported); // "IsImported property was not set."
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
            Assert.ContainsSingle(items); // "Wrong number of items in the project."

            ProjectItem item = items.First();
            Assert.IsNull(item.Xml.ContainingProject.FullPath); // "Item was not found in the project." // null because XML is in-memory

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
            Assert.ContainsSingle(metadatum); // "Incorrect number of imported metadata found."

            ProjectMetadata metadata = metadatum.First();
            Assert.IsFalse(metadata.IsImported); // "IsImport property is set."

            return metadata;
        }

        #endregion
    }
}
