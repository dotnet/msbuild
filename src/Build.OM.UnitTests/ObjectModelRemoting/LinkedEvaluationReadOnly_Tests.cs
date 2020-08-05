// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Schema;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Sdk;

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
                this.BigFile = this.ImmutableDisk.WriteProjectFile($"Big.proj", TestCollectionGroup.BigProjectFile);
                var projReal = this.Remote[0].LoadProjectWithSettings(this.BigFile, ProjectLoadSettings.IgnoreMissingImports | ProjectLoadSettings.RecordDuplicateButNotCircularImports);
                this.Local.Importing = true;
                Assert.NotNull(projReal);
                this.Real = projReal;
                Assert.NotNull(this.Real);
                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                this.View = projView;

                ViewValidation.VerifyNotLinkedNotNull(this.Real);
                ViewValidation.VerifyLinkedNotNull(this.View);
            }

            public void ResetBeforeTests()
            {
                this.Group.ClearAllRemotes();

                var projView = this.Local.GetLoadedProjects(this.BigFile).FirstOrDefault();
                Assert.NotNull(projView);
                Assert.NotSame(projView, this.View);
                this.View = projView;

                ViewValidation.VerifyLinkedNotNull(this.View);
            }
        }

        private ROTestCollectionGroup StdGroup { get; }

        public LinkedEvaluationReadOnly_Tests(ROTestCollectionGroup group)
        {
            this.StdGroup = group;
            this.StdGroup.ResetBeforeTests();
        }

        [Fact]
        public void ProjectReadOnly_Tests()
        {
            // this is actually very elaborate and caught quite a few issues.
            ViewValidation.Verify(this.StdGroup.View, this.StdGroup.Real);
        }

        [Fact]
        public void ProjectItemReadOnly_Tests()
        {
            var viewItems = this.StdGroup.View.AllEvaluatedItems;
            var realItems = this.StdGroup.Real.AllEvaluatedItems;

            Assert.NotEmpty(viewItems);
            ViewValidation.Verify(viewItems, realItems);
        }

        [Fact]
        public void ProjectItemDefinitionReadOnly_Tests()
        {
            var viewItemDefinitions = this.StdGroup.View.ItemDefinitions;
            var realItemDefinitions = this.StdGroup.Real.ItemDefinitions;

            Assert.NotEmpty(viewItemDefinitions);
            ViewValidation.Verify(viewItemDefinitions, realItemDefinitions, ViewValidation.Verify);
        }

        [Fact]
        public void ProjectPropertiesReadOnly_Tests()
        {
            var viewProperties = this.StdGroup.View.Properties;
            var realProperties = this.StdGroup.Real.Properties;

            Assert.NotEmpty(viewProperties);
            ViewValidation.Verify(viewProperties, realProperties);
        }

        [Fact]
        public void ProjectMetadataReadOnly_Tests()
        {
            var viewMetadata = this.StdGroup.View.AllEvaluatedItemDefinitionMetadata;
            var realMetadata = this.StdGroup.Real.AllEvaluatedItemDefinitionMetadata;

            Assert.NotEmpty(viewMetadata);
            ViewValidation.Verify(viewMetadata, realMetadata);
        }
    }
}
