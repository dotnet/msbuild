// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Linq;
    using Microsoft.Build.Construction;

    /// <summary>
    /// Most importantly we want to touch implementation to all public method to catch any
    /// potential transitional error.
    ///
    /// Since we have 2 independent views of the same object we have the "luxury" to do a full complete validation.
    /// </summary>
    [TestClass]
    public class LinkedConstructionReadOnly_Tests
    {
        public class ROTestCollectionGroup : TestCollectionGroup
        {
            public string BigFile { get; }
            public ProjectRootElement RealXml { get; }
            public ProjectRootElement ViewXml { get; private set; }

            public ROTestCollectionGroup()
                : base(1, 0)
            {
                this.BigFile = this.ImmutableDisk.WriteProjectFile($"Big.proj", TestCollectionGroup.BigProjectFile);
                var projReal = this.Remote[0].LoadProjectIgnoreMissingImports(this.BigFile);
                this.Local.Importing = true;
                Assert.IsNotNull(projReal);
                this.RealXml = projReal.Xml;
                Assert.IsNotNull(this.RealXml);
                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.IsNotNull(projView);
                this.ViewXml = projView.Xml;

                ViewValidation.VerifyNotLinkedNotNull(this.RealXml);
                ViewValidation.VerifyLinkedNotNull(this.ViewXml);
            }

            public void ResetBeforeTests()
            {
                this.Group.ClearAllRemotes();

                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.IsNotNull(projView);
                Assert.AreNotSame<object>(projView, this.ViewXml);
                this.ViewXml = projView.Xml;

                ViewValidation.VerifyLinkedNotNull(this.ViewXml);
            }
        }

        private ROTestCollectionGroup StdGroup { get; }

        private static ROTestCollectionGroup s_stdGroup;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) => s_stdGroup = new ROTestCollectionGroup();

        [ClassCleanup]
        public static void ClassCleanup() => s_stdGroup?.Dispose();

        public LinkedConstructionReadOnly_Tests()
        {
            this.StdGroup = s_stdGroup; // new ROTestCollectionGroup();
            this.StdGroup.ResetBeforeTests();
            // group.Clear();
        }


        [MSBuildTestMethod]
        public void ProjectRootElemetReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            ViewValidation.Verify(preView, preReal);
        }

        [MSBuildTestMethod]
        public void ProjectChooseElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            Assert.IsNotEmpty(preReal.ChooseElements);

            ViewValidation.Verify(preView.ChooseElements, preReal.ChooseElements, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectExtensionsElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realExtensionsList = preReal.ChildrenReversed.OfType<ProjectExtensionsElement>().ToList();
            var viewExtensionsList = preView.ChildrenReversed.OfType<ProjectExtensionsElement>().ToList();

            Assert.IsNotEmpty(realExtensionsList);

            ViewValidation.Verify(viewExtensionsList, realExtensionsList, ViewValidation.Verify);

            var realXml = realExtensionsList.FirstOrDefault();
            var viewXml = viewExtensionsList.FirstOrDefault();

            Assert.AreEqual(realXml["a"], viewXml["a"]);
            Assert.AreEqual(realXml["b"], viewXml["b"]);
            Assert.AreEqual("x", viewXml["a"]);
            Assert.AreEqual("y", viewXml["b"]);
        }

        [MSBuildTestMethod]
        public void ProjectImportElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realImports = preReal.Imports.ToList();
            var viewImports = preView.Imports.ToList();

            Assert.IsNotEmpty(realImports);
            ViewValidation.Verify(viewImports, realImports, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectImportGroupElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realImportGroups = preReal.ImportGroups.ToList();
            var viewImportGroups = preView.ImportGroups.ToList();

            Assert.IsNotEmpty(realImportGroups);
            ViewValidation.Verify(viewImportGroups, realImportGroups, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectItemDefinitionElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realItemDefinitions = preReal.ItemDefinitions.ToList();
            var viewlItemDefinitions = preView.ItemDefinitions.ToList();

            Assert.IsNotEmpty(realItemDefinitions);
            ViewValidation.Verify(viewlItemDefinitions, realItemDefinitions);
        }

        [MSBuildTestMethod]
        public void ProjectItemDefinitionGroupElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realItemDefinitionGroups = preReal.ItemDefinitionGroups.ToList();
            var viewlItemDefinitionGroups = preView.ItemDefinitionGroups.ToList();

            Assert.IsNotEmpty(realItemDefinitionGroups);
            ViewValidation.Verify(viewlItemDefinitionGroups, realItemDefinitionGroups, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectItemElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realItems = preReal.Items.ToList();
            var viewlItems = preView.Items.ToList();

            Assert.IsNotEmpty(realItems);
            ViewValidation.Verify(viewlItems, realItems, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectItemGroupElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realItemGroups = preReal.ItemGroups.ToList();
            var viewItemGroups = preView.ItemGroups.ToList();

            Assert.IsNotEmpty(realItemGroups);
            ViewValidation.Verify(viewItemGroups, realItemGroups, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectPropertyElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realProperties = preReal.Properties.ToList();
            var viewProperties = preView.Properties.ToList();

            Assert.IsNotEmpty(realProperties);
            ViewValidation.Verify(viewProperties, realProperties, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectPropertyGroupElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realPropertieGroups = preReal.PropertyGroups.ToList();
            var viewPropertieGroups = preView.PropertyGroups.ToList();

            Assert.IsNotEmpty(realPropertieGroups);
            ViewValidation.Verify(viewPropertieGroups, realPropertieGroups, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectOtherwiseElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOtherwiseElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOtherwiseElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectProjectWhenElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectWhenElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectWhenElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectProjectSdkElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectSdkElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectSdkElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectTargetElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.Targets.ToList();
            var viewCollection = preView.Targets.ToList();

            Assert.IsNotEmpty(realCollection);  // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectTaskElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectTaskElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectTaskElement>().ToList();

            Assert.IsNotEmpty(realCollection);  // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        // Also validates:
        [MSBuildTestMethod]
        public void ProjectUsingTaskElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskElement>().ToList();

            Assert.IsNotEmpty(realCollection); // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectUsingTaskBodyElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskBodyElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskBodyElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void UsingTaskParameterGroupElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<UsingTaskParameterGroupElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<UsingTaskParameterGroupElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectUsingTaskParameterElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskParameterElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskParameterElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectOnErrorElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOnErrorElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOnErrorElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectOutputElementReadOnly()
        {
            var preReal = this.StdGroup.RealXml;
            var preView = this.StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOutputElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOutputElement>().ToList();

            Assert.IsNotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }
    }
}
