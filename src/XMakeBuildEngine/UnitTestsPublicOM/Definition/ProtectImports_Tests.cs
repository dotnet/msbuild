//-----------------------------------------------------------------------
// <copyright file="ProtectImports_Tests.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>Tests for protecting imported files while editing</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.UnitTests.OM.Definition
{
    /// <summary>
    /// Tests for protecting imported files while editing
    /// </summary>
    [TestClass]
    public class ProtectImports_Tests
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
        private string importFilename;

        #region Test lifetime

        /// <summary>
        /// Configures the overall test.
        /// </summary>
        [TestInitialize]
        public void Setup()
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
            importFilename = Microsoft.Build.Shared.FileUtilities.GetTemporaryFile() + ".targets";
            File.WriteAllText(importFilename, importContents);
        }

        /// <summary>
        /// Undoes the test configuration.
        /// </summary>
        [TestCleanup]
        public void Teardown()
        {
            if (File.Exists(importFilename))
            {
                File.Delete(importFilename);
            }
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests against edits into imported properties thru the property itself.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PropertySetViaProperty()
        {
            Project project = GetProject();
            ProjectProperty property = GetProperty(project);

            // This should throw
            property.UnevaluatedValue = NewValue;
        }

        /// <summary>
        /// Tests against edits into imported properties thru the project.
        /// Instead of editing the existing property, because it originated
        /// in an imported file, it should create a new one in the main project.
        /// </summary>
        [TestMethod]
        public void PropertySetViaProject()
        {
            Project project = GetProject();
            ProjectProperty property = GetProperty(project);

            project.SetProperty(PropertyName, NewValue);

            Assert.AreEqual(NewValue, project.GetPropertyValue(PropertyName));
        }

        /// <summary>
        /// Tests against edits into imported properties thru the property itself.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void PropertyRemove()
        {
            Project project = GetProject();
            ProjectProperty property = GetProperty(project);

            // This should throw
            project.RemoveProperty(property);
        }

        #endregion

        #region Item Tests

        /// <summary>
        /// Tests imported item type change.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ItemImportedChangeType()
        {            
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.ItemType = "NewItemType";
        }

        /// <summary>
        /// Tests imported item renaming.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ItemImportedRename()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.Rename("NewItemName");
        }
        
        /// <summary>
        /// Tests imported item SetUnevaluatedValue.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ItemImportedSetUnevaluatedValue()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.UnevaluatedInclude = "NewItemName";
        }

        /// <summary>
        /// Tests imported item removal.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ItemImportedRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            project.RemoveItem(item);
        }

        /// <summary>
        /// Tests project item type change.
        /// </summary>
        [TestMethod]
        public void ItemProjectChangeType()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.ItemType = NewValue;
            Assert.IsTrue(project.GetItems(NewValue).Count() == 1, "Item in project didn't change name");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests project item renaming.
        /// </summary>
        [TestMethod]
        public void ItemProjectRename()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.Rename(NewValue);
            Assert.AreEqual(NewValue, item.EvaluatedInclude, "Item in project didn't change name.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests project item SetUnevaluatedValue.
        /// </summary>
        [TestMethod]
        public void ItemProjectSetUnevaluatedValue()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.UnevaluatedInclude = NewValue;
            Assert.AreEqual(NewValue, item.EvaluatedInclude, "Item in project didn't change name.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests project item removal.
        /// </summary>
        [TestMethod]
        public void ItemProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            project.RemoveItem(item);
            Assert.IsTrue(project.GetItems(ItemType).Count() == 1, "Item in project wasn't removed.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        #endregion

        #region Metadata Tests

        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MetadataImportSetViaProject()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.SetMetadataValue(ImportedMetadataName, "NewImportedMetadataValue");
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MetadataImportAdd()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.SetMetadataValue("NewMetadata", "NewImportedMetadataValue");
        }
        
        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MetadataImportSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetImportedMetadata(project);
            
            // This should throw
            metadata.UnevaluatedValue = NewValue;
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void MetadataImportRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetImportedItem(project);

            // This should throw
            item.RemoveMetadata(ImportedMetadataName);
        }

        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [TestMethod]
        public void MetadataProjectSetViaItem()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(ProjectMetadataName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(ProjectMetadataName), "Metadata not saved correctly in project.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        public void MetadataProjectAdd()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            string newName = "NewMetadata";
            item.SetMetadataValue(newName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(newName), "Metadata not saved correctly in project.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        public void MetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetProjectMetadata(project);

            string newValue = "NewProjectMetadataValue";
            metadata.UnevaluatedValue = newValue;

            Assert.AreEqual(newValue, metadata.EvaluatedValue);
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [TestMethod]
        public void MetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(ProjectMetadataName);
            Assert.IsFalse(item.HasMetadata(ProjectMetadataName), "Metadata was not removed from project.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        #endregion

        #region Metadata in Item Definition Tests

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DefinitionMetadataImportSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetNonOverridableMetadata(project);

            // This should throw
            metadata.UnevaluatedValue = NewValue;
        }

        /// <summary>
        /// Tests removing metadata in imported item definition.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void DefinitionMetadataImportRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            // This should throw
            item.RemoveMetadata(NonOverridableMetadataName);
        }

        /// <summary>
        /// Tests setting existing metadata in import.
        /// </summary>
        [TestMethod]
        public void DefinitionMetadataProjectSetViaProject()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.SetMetadataValue(OverridableMetadataName, NewValue);
            Assert.AreEqual(NewValue, item.GetMetadataValue(OverridableMetadataName), "Metadata not set correctly in project.");
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests setting new metadata in import.
        /// </summary>
        [TestMethod]
        public void DefinitionMetadataProjectSetViaMetadata()
        {
            Project project = GetProject();
            ProjectMetadata metadata = GetOverridableMetadata(project);

            metadata.UnevaluatedValue = NewValue;
            Assert.AreEqual(NewValue, metadata.EvaluatedValue);
            Assert.IsTrue(project.IsDirty, "Project was not marked dirty.");
        }

        /// <summary>
        /// Tests removing metadata in import.
        /// </summary>
        [TestMethod]
        public void DefinitionMetadataProjectRemove()
        {
            Project project = GetProject();
            ProjectItem item = GetProjectItem(project);

            item.RemoveMetadata(OverridableMetadataName);

            ProjectMetadata metadata = item.GetMetadata(OverridableMetadataName);
            Assert.IsNotNull(metadata, "Imported metadata not found after the project's one was removed.");
            Assert.IsTrue(metadata.IsImported, "IsImported property is not set.");
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
            string expanded = original.Replace("$importFilename", importFilename);
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
            Assert.IsTrue(items.Count() == 1, "Wrong number of items in the import.");

            ProjectItem item = items.First();
            Assert.AreEqual(importFilename, item.Xml.ContainingProject.FullPath, "Item was not found in the imported project.");

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
            Assert.IsTrue(metadatum.Count() == 1, "Incorrect number of imported metadata found.");

            ProjectMetadata metadata = metadatum.First();
            Assert.IsTrue(metadata.IsImported, "IsImport property is not set.");

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
            Assert.IsTrue(metadatum.Count() == 1, "Incorrect number of imported metadata found.");

            ProjectMetadata metadata = metadatum.First();
            Assert.IsTrue(metadata.IsImported, "IsImport property is not set.");

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
            Assert.IsTrue(metadatum.Count() == 1, "Incorrect number of imported metadata found.");

            ProjectMetadata metadata = metadatum.First();
            Assert.IsFalse(metadata.IsImported, "IsImport property is set.");

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
            Assert.AreEqual(importFilename, property.Xml.ContainingProject.FullPath, "Property was not found in the imported project.");
            Assert.IsTrue(property.IsImported, "IsImported property was not set.");
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
            Assert.IsTrue(items.Count() == 1, "Wrong number of items in the project.");

            ProjectItem item = items.First();
            Assert.AreEqual(null, item.Xml.ContainingProject.FullPath, "Item was not found in the project."); // null because XML is in-memory

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
            Assert.IsTrue(metadatum.Count() == 1, "Incorrect number of imported metadata found.");

            ProjectMetadata metadata = metadatum.First();
            Assert.IsFalse(metadata.IsImported, "IsImport property is set.");

            return metadata;
        }

        #endregion
    }
}
