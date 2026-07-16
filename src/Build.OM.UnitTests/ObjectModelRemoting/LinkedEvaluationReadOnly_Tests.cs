// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Linq;
    using Microsoft.Build.Evaluation;

    [TestClass]
    public class LinkedEvaluationReadOnly_Tests
    {
        public class ROTestCollectionGroup : TestCollectionGroup
        {
            public string BigFile { get; }
            public Project Real { get; }
            public Project View { get; private set; }

            public ROTestCollectionGroup()
                : base(1, 0)
            {
                this.BigFile = this.ImmutableDisk.WriteProjectFile($"Big.proj", TestCollectionGroup.BigProjectFile);
                var projReal = this.Remote[0].LoadProjectWithSettings(this.BigFile, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.RecordDuplicateButNotCircularImports);
                this.Local.Importing = true;
                Assert.IsNotNull(projReal);
                this.Real = projReal;
                Assert.IsNotNull(this.Real);
                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.IsNotNull(projView);
                this.View = projView;

                ViewValidation.VerifyNotLinkedNotNull(this.Real);
                ViewValidation.VerifyLinkedNotNull(this.View);
            }

            public void ResetBeforeTests()
            {
                this.Group.ClearAllRemotes();

                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.IsNotNull(projView);
                Assert.AreNotSame(projView, this.View);
                this.View = projView;

                ViewValidation.VerifyLinkedNotNull(this.View);
            }
        }

        private ROTestCollectionGroup StdGroup { get; }

        private static ROTestCollectionGroup s_stdGroup;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) => s_stdGroup = new ROTestCollectionGroup();

        [ClassCleanup]
        public static void ClassCleanup() => s_stdGroup?.Dispose();

        public LinkedEvaluationReadOnly_Tests()
        {
            this.StdGroup = s_stdGroup;
            this.StdGroup.ResetBeforeTests();
        }

        [MSBuildTestMethod]
        public void ProjectReadOnly_Tests()
        {
            // this is actually very elaborate and caught quite a few issues.
            ViewValidation.Verify(this.StdGroup.View, this.StdGroup.Real);
        }

        [MSBuildTestMethod]
        public void ProjectItemReadOnly_Tests()
        {
            var viewItems = this.StdGroup.View.AllEvaluatedItems;
            var realItems = this.StdGroup.Real.AllEvaluatedItems;

            Assert.IsNotEmpty(viewItems);
            ViewValidation.Verify(viewItems, realItems);
        }

        [MSBuildTestMethod]
        public void ProjectItemDefinitionReadOnly_Tests()
        {
            var viewItemDefinitions = this.StdGroup.View.ItemDefinitions;
            var realItemDefinitions = this.StdGroup.Real.ItemDefinitions;

            Assert.IsNotEmpty(viewItemDefinitions);
            ViewValidation.Verify(viewItemDefinitions, realItemDefinitions, ViewValidation.Verify);
        }

        [MSBuildTestMethod]
        public void ProjectPropertiesReadOnly_Tests()
        {
            var viewProperties = this.StdGroup.View.Properties;
            var realProperties = this.StdGroup.Real.Properties;

            Assert.IsNotEmpty(viewProperties);
            ViewValidation.Verify(viewProperties, realProperties);
        }

        [MSBuildTestMethod]
        public void ProjectMetadataReadOnly_Tests()
        {
            var viewMetadata = this.StdGroup.View.AllEvaluatedItemDefinitionMetadata;
            var realMetadata = this.StdGroup.Real.AllEvaluatedItemDefinitionMetadata;

            Assert.IsNotEmpty(viewMetadata);
            ViewValidation.Verify(viewMetadata, realMetadata);
        }
    }
}
