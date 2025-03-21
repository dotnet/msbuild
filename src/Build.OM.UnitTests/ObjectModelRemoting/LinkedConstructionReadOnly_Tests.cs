// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Linq;
    using Microsoft.Build.Construction;
    using Xunit;

    /// <summary>
    /// Most importantly we want to touch implementation to all public method to catch any
    /// potential transitional error.
    ///
    /// Since we have 2 independent views of the same object we have the "luxury" to do a full complete validation.
    /// </summary>
    public class LinkedConstructionReadOnly_Tests : IClassFixture<LinkedConstructionReadOnly_Tests.ROTestCollectionGroup>
    {
        public class ROTestCollectionGroup : TestCollectionGroup
        {
            public string BigFile { get; }
            public ProjectRootElement RealXml { get; }
            public ProjectRootElement ViewXml { get; private set; }

            public ROTestCollectionGroup()
                : base(1, 0)
            {
                BigFile = ImmutableDisk.WriteProjectFile($"Big.proj", BigProjectFile);
                var projReal = Remote[0].LoadProjectIgnoreMissingImports(BigFile);
                Local.Importing = true;
                Assert.NotNull(projReal);
                RealXml = projReal.Xml;
                Assert.NotNull(RealXml);
                var projView = Local.GetLoadedProjects(BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                ViewXml = projView.Xml;

                ViewValidation.VerifyNotLinkedNotNull(RealXml);
                ViewValidation.VerifyLinkedNotNull(ViewXml);
            }

            public void ResetBeforeTests()
            {
                Group.ClearAllRemotes();

                var projView = Local.GetLoadedProjects(BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                Assert.NotSame(projView, ViewXml);
                ViewXml = projView.Xml;

                ViewValidation.VerifyLinkedNotNull(ViewXml);
            }
        }

        private ROTestCollectionGroup StdGroup { get; }

        public LinkedConstructionReadOnly_Tests(ROTestCollectionGroup group)
        {
            StdGroup = group; // new ROTestCollectionGroup();
            StdGroup.ResetBeforeTests();
            // group.Clear();
        }


        [Fact]
        public void ProjectRootElemetReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            ViewValidation.Verify(preView, preReal);
        }

        [Fact]
        public void ProjectChooseElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            Assert.NotEmpty(preReal.ChooseElements);

            ViewValidation.Verify(preView.ChooseElements, preReal.ChooseElements, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectExtensionsElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realExtensionsList = preReal.ChildrenReversed.OfType<ProjectExtensionsElement>().ToList();
            var viewExtensionsList = preView.ChildrenReversed.OfType<ProjectExtensionsElement>().ToList();

            Assert.NotEmpty(realExtensionsList);

            ViewValidation.Verify(viewExtensionsList, realExtensionsList, ViewValidation.Verify);

            var realXml = realExtensionsList.FirstOrDefault();
            var viewXml = viewExtensionsList.FirstOrDefault();

            Assert.Equal(realXml["a"], viewXml["a"]);
            Assert.Equal(realXml["b"], viewXml["b"]);
            Assert.Equal("x", viewXml["a"]);
            Assert.Equal("y", viewXml["b"]);
        }

        [Fact]
        public void ProjectImportElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realImports = preReal.Imports.ToList();
            var viewImports = preView.Imports.ToList();

            Assert.NotEmpty(realImports);
            ViewValidation.Verify(viewImports, realImports, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectImportGroupElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realImportGroups = preReal.ImportGroups.ToList();
            var viewImportGroups = preView.ImportGroups.ToList();

            Assert.NotEmpty(realImportGroups);
            ViewValidation.Verify(viewImportGroups, realImportGroups, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectItemDefinitionElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realItemDefinitions = preReal.ItemDefinitions.ToList();
            var viewlItemDefinitions = preView.ItemDefinitions.ToList();

            Assert.NotEmpty(realItemDefinitions);
            ViewValidation.Verify(viewlItemDefinitions, realItemDefinitions);
        }

        [Fact]
        public void ProjectItemDefinitionGroupElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realItemDefinitionGroups = preReal.ItemDefinitionGroups.ToList();
            var viewlItemDefinitionGroups = preView.ItemDefinitionGroups.ToList();

            Assert.NotEmpty(realItemDefinitionGroups);
            ViewValidation.Verify(viewlItemDefinitionGroups, realItemDefinitionGroups, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectItemElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realItems = preReal.Items.ToList();
            var viewlItems = preView.Items.ToList();

            Assert.NotEmpty(realItems);
            ViewValidation.Verify(viewlItems, realItems, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectItemGroupElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realItemGroups = preReal.ItemGroups.ToList();
            var viewItemGroups = preView.ItemGroups.ToList();

            Assert.NotEmpty(realItemGroups);
            ViewValidation.Verify(viewItemGroups, realItemGroups, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectPropertyElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realProperties = preReal.Properties.ToList();
            var viewProperties = preView.Properties.ToList();

            Assert.NotEmpty(realProperties);
            ViewValidation.Verify(viewProperties, realProperties, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectPropertyGroupElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realPropertieGroups = preReal.PropertyGroups.ToList();
            var viewPropertieGroups = preView.PropertyGroups.ToList();

            Assert.NotEmpty(realPropertieGroups);
            ViewValidation.Verify(viewPropertieGroups, realPropertieGroups, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectOtherwiseElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOtherwiseElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOtherwiseElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectProjectWhenElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectWhenElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectWhenElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectProjectSdkElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectSdkElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectSdkElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectTargetElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.Targets.ToList();
            var viewCollection = preView.Targets.ToList();

            Assert.NotEmpty(realCollection);  // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectTaskElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectTaskElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectTaskElement>().ToList();

            Assert.NotEmpty(realCollection);  // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        // Also validates:
        [Fact]
        public void ProjectUsingTaskElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskElement>().ToList();

            Assert.NotEmpty(realCollection); // to ensure we actually have some elements in test project
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectUsingTaskBodyElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskBodyElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskBodyElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void UsingTaskParameterGroupElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<UsingTaskParameterGroupElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<UsingTaskParameterGroupElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectUsingTaskParameterElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectUsingTaskParameterElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectUsingTaskParameterElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectOnErrorElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOnErrorElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOnErrorElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectOutputElementReadOnly()
        {
            var preReal = StdGroup.RealXml;
            var preView = StdGroup.ViewXml;

            var realCollection = preReal.AllChildren.OfType<ProjectOutputElement>().ToList();
            var viewCollection = preView.AllChildren.OfType<ProjectOutputElement>().ToList();

            Assert.NotEmpty(realCollection);
            ViewValidation.Verify(viewCollection, realCollection, ViewValidation.Verify);
        }
    }
}
