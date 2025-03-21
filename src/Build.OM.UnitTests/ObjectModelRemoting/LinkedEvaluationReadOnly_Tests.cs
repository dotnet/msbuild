// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using Xunit;

    public class LinkedEvaluationReadOnly_Tests : IClassFixture<LinkedEvaluationReadOnly_Tests.ROTestCollectionGroup>
    {
        public class ROTestCollectionGroup : TestCollectionGroup
        {
            public string BigFile { get; }
            public Project Real { get; }
            public Project View { get; private set; }

            public ROTestCollectionGroup()
                : base(1, 0)
            {
                BigFile = ImmutableDisk.WriteProjectFile($"Big.proj", BigProjectFile);
                var projReal = Remote[0].LoadProjectWithSettings(BigFile, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.RecordDuplicateButNotCircularImports);
                Local.Importing = true;
                Assert.NotNull(projReal);
                Real = projReal;
                Assert.NotNull(Real);
                var projView = Local.GetLoadedProjects(BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                View = projView;

                ViewValidation.VerifyNotLinkedNotNull(Real);
                ViewValidation.VerifyLinkedNotNull(View);
            }

            public void ResetBeforeTests()
            {
                Group.ClearAllRemotes();

                var projView = Local.GetLoadedProjects(BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                Assert.NotSame(projView, View);
                View = projView;

                ViewValidation.VerifyLinkedNotNull(View);
            }
        }

        private ROTestCollectionGroup StdGroup { get; }

        public LinkedEvaluationReadOnly_Tests(ROTestCollectionGroup group)
        {
            StdGroup = group;
            StdGroup.ResetBeforeTests();
        }

        [Fact]
        public void ProjectReadOnly_Tests()
        {
            // this is actually very elaborate and caught quite a few issues.
            ViewValidation.Verify(StdGroup.View, StdGroup.Real);
        }

        [Fact]
        public void ProjectItemReadOnly_Tests()
        {
            var viewItems = StdGroup.View.AllEvaluatedItems;
            var realItems = StdGroup.Real.AllEvaluatedItems;

            Assert.NotEmpty(viewItems);
            ViewValidation.Verify(viewItems, realItems);
        }

        [Fact]
        public void ProjectItemDefinitionReadOnly_Tests()
        {
            var viewItemDefinitions = StdGroup.View.ItemDefinitions;
            var realItemDefinitions = StdGroup.Real.ItemDefinitions;

            Assert.NotEmpty(viewItemDefinitions);
            ViewValidation.Verify(viewItemDefinitions, realItemDefinitions, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectPropertiesReadOnly_Tests()
        {
            var viewProperties = StdGroup.View.Properties;
            var realProperties = StdGroup.Real.Properties;

            Assert.NotEmpty(viewProperties);
            ViewValidation.Verify(viewProperties, realProperties);
        }

        [Fact]
        public void ProjectMetadataReadOnly_Tests()
        {
            var viewMetadata = StdGroup.View.AllEvaluatedItemDefinitionMetadata;
            var realMetadata = StdGroup.Real.AllEvaluatedItemDefinitionMetadata;

            Assert.NotEmpty(viewMetadata);
            ViewValidation.Verify(viewMetadata, realMetadata);
        }
    }
}
