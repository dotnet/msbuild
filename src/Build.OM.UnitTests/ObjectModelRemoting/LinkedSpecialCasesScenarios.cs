// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.ObjectModelRemoting;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.UnitTests.OM.Construction;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.NetCore.Extensions;
    using Xunit.Sdk;

    public class LinkedSpecialCasesScenarios : IClassFixture<LinkedSpecialCasesScenarios.MyTestCollectionGroup>
    {
        public class MyTestCollectionGroup : TestCollectionGroup
        {
            public string LocalBigPath { get; }
            public string TargetBigPath { get; }
            public string GuestBigPath { get; }

            public Project LocalBig { get; }
            public Project TargetBig { get; }
            public Project GuestBig { get; }

            internal ProjectCollectionLinker Target { get; }
            internal ProjectCollectionLinker Guest { get; }

            internal ProjectXmlPair TargetXmlPair { get; private set; }
            internal ProjectXmlPair GuestXmlPair { get; private set; }

            public MyTestCollectionGroup()
                : base(2, 0)
            {
                this.LocalBigPath = this.ImmutableDisk.WriteProjectFile($"BigLocal.proj", TestCollectionGroup.BigProjectFile);
                this.TargetBigPath = this.ImmutableDisk.WriteProjectFile($"BigTarget.proj", TestCollectionGroup.BigProjectFile);
                this.GuestBigPath = this.ImmutableDisk.WriteProjectFile($"BigGuest.proj", TestCollectionGroup.BigProjectFile);

                this.Target = this.Remote[0];
                this.Guest = this.Remote[1];

                this.LocalBig = this.Local.LoadProjectIgnoreMissingImports(this.LocalBigPath);
                this.TargetBig = this.Target.LoadProjectIgnoreMissingImports(this.TargetBigPath);
                this.GuestBig = this.Guest.LoadProjectIgnoreMissingImports(this.GuestBigPath);

                this.TakeSnapshot();
            }

            public void ResetBeforeTests()
            {
                this.Clear();
                this.Local.Importing = true;
                {
                    var targetView = this.Local.GetLoadedProjects(this.TargetBigPath).FirstOrDefault();
                    Assert.NotNull(targetView);
                    var targetPair = new ProjectPair(targetView, this.TargetBig);
                    this.TargetXmlPair = new ProjectXmlPair(targetPair);
                }

                {
                    var guestView = this.Local.GetLoadedProjects(this.GuestBigPath).FirstOrDefault();
                    Assert.NotNull(guestView);
                    var guestPair = new ProjectPair(guestView, this.GuestBig);
                    this.GuestXmlPair = new ProjectXmlPair(guestPair);
                }
            }
        }

        public MyTestCollectionGroup StdGroup { get; }
        public LinkedSpecialCasesScenarios(MyTestCollectionGroup group)
        {

            this.StdGroup = group;
            group.ResetBeforeTests();
        }

        private ProjectPair GetNewInMemoryProject(string path, string content = null)
        {
            content = content ?? TestCollectionGroup.SampleProjectFile;
            var tempPath = this.StdGroup.Disk.GetAbsolutePath(path);
            var newReal = this.StdGroup.Target.LoadInMemoryWithSettings(content, ProjectLoadSettings.IgnoreMissingImports);
            newReal.Xml.FullPath = tempPath;
            var newView = this.StdGroup.Local.GetLoadedProjects(tempPath).FirstOrDefault();
            Assert.NotNull(newView);

            ViewValidation.Verify(newView, newReal);

            return new ProjectPair(newView, newReal);
        }

        private void CloneAndAddInternal(ProjectRootElement sourceProject)
        {
            bool externalSource = sourceProject != null;

            var projectPair = GetNewInMemoryProject("Clone", TestCollectionGroup.BigProjectFile);
            var xmlPair = new ProjectXmlPair(projectPair);

            Assert.True(xmlPair.View.HasUnsavedChanges);
            xmlPair.View.Save();
            Assert.False(xmlPair.View.HasUnsavedChanges);

            sourceProject = sourceProject ?? xmlPair.View;


            // var existingItemGroup1 = sourceProject.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1");
            var existingItemGroupList = sourceProject.AllChildren.OfType<ProjectItemGroupElement>().Where(((ig) => ig.Label == "Group1")).ToList();
            Assert.Single(existingItemGroupList);
            var existingItemGroup = existingItemGroupList[0];

            var cloned = (ProjectItemGroupElement)existingItemGroup.Clone();
            Assert.NotSame(cloned, existingItemGroup);
            Assert.False(sourceProject.HasUnsavedChanges);

            var sourceIsALink = ViewValidation.IsLinkedObject(sourceProject);
            ViewValidation.VerifyNotNull(cloned, sourceIsALink);


            if (externalSource)
            {
                Assert.ThrowsAny<InvalidOperationException>(() => xmlPair.View.AppendChild(cloned));
            }
            else
            {
                var clonedPair = xmlPair.CreateFromView(cloned);
                xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig == cloned || ig == clonedPair.Real, 0);

                xmlPair.View.AppendChild(cloned);
                Assert.True(xmlPair.View.HasUnsavedChanges);
                Assert.True(xmlPair.Real.HasUnsavedChanges);

                clonedPair.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig == clonedPair.View || ig == clonedPair.Real));
                xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1", 2);

                clonedPair.VerifySetter("Group2", (g) => g.Label, (g, v) => g.Label = v);
                xmlPair.Verify();

                Assert.Equal("Group1", existingItemGroup.Label);
            }
        }


        private void CopyFromInternal(ProjectRootElement sourceProject)
        {
            // quite a few complexity in the ExternalProjectProvider implementation is because of
            // ProjectElement.CopyFrom and ProjectElementContainer.DeepCopyFrom....

            bool externalSource = sourceProject != null;

            var projectPair = GetNewInMemoryProject("CopyFrom", TestCollectionGroup.BigProjectFile);
            var xmlPair = new ProjectXmlPair(projectPair);

            Assert.True(xmlPair.View.HasUnsavedChanges);
            xmlPair.View.Save();
            Assert.False(xmlPair.View.HasUnsavedChanges);

            sourceProject = sourceProject ?? xmlPair.View;

            var existingItemGroupList = sourceProject.AllChildren.OfType<ProjectItemGroupElement>().Where(((ig) => ig.Label == "Group1")).ToList();
            Assert.Single(existingItemGroupList);
            var existingItemGroup = existingItemGroupList[0];
            Assert.NotNull(existingItemGroup);
            var realExistingItemGroup = ViewValidation.GetRealObject(existingItemGroup);

            var ourGroup1 = xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1");

            var newCopyFrom = xmlPair.AddNewLabeledChaildWithVerify<ProjectItemGroupElement>(ObjectType.View, "newGrop", (p, l) => p.AddItemGroup());

            newCopyFrom.View.CopyFrom(existingItemGroup);
            xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1", 2);
            newCopyFrom.View.Label = "CopyFrom";
            newCopyFrom.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "CopyFrom"));
            ourGroup1.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1"));
            // children are not copied.
            Assert.Empty(newCopyFrom.View.Items);
            // but attributes are (even non standard)
            //Assert.Equal("2", ProjectElementLink.GetAttributeValue(existingItemGroup, "FunnyAttribute", true));
            //Assert.Equal("2", ProjectElementLink.GetAttributeValue(newCopyFrom.View, "FunnyAttribute", true));
            newCopyFrom.VerifyNotSame(ourGroup1);


            Assert.True(xmlPair.View.HasUnsavedChanges);
            Assert.False(externalSource && sourceProject.HasUnsavedChanges);

            var newDeepCopy = xmlPair.AddNewLabeledChaildWithVerify<ProjectItemGroupElement>(ObjectType.View, "newGrop", (p, l) => p.AddItemGroup());
            newDeepCopy.View.DeepCopyFrom(existingItemGroup);

            xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1", 2);
            // slightly cheting but we know that the large groups should be the same, even though there are not the same object
            // note do that before changing the label.
            Assert.NotSame(realExistingItemGroup, newDeepCopy.Real);
            // TODO XmlLocation is (correctly) different for the items, need to find a way to bypass it.
            var context = new ValidationContext();
            context.ValidateLocation = delegate (ElementLocation a, ElementLocation e) { return;};

            ViewValidation.Verify(newDeepCopy.View, realExistingItemGroup, context);
            newDeepCopy.View.Label = "DeepCopyFrom";
            newDeepCopy.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "DeepCopyFrom"));
            ourGroup1.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1"));
            newDeepCopy.VerifyNotSame(ourGroup1);

            Assert.False(externalSource && sourceProject.HasUnsavedChanges);
        }

        [Fact]
        public void CloneAndAddInnerProject()
        {
            CloneAndAddInternal(null);
        }

        [Fact]
        public void CloneAndAddCrossProjectSameCollection()
        {
            // view gets "a view" object as argument from different project (but in the same collection)
            CloneAndAddInternal(this.StdGroup.TargetXmlPair.View);
        }

        [Fact]
        public void CloneAndAddCrossProjectLocalSource()
        {
            // view gets "a real" object as argument
            CloneAndAddInternal(this.StdGroup.LocalBig.Xml);
        }

        [Fact]
        public void CloneAndAddCrossProjectCrossCollection()
        {
            // view gets "a view" object as argument from different project and collection (double proxy)
            CloneAndAddInternal(this.StdGroup.GuestXmlPair.View);
        }


        [Fact]
        public void CopyFromInnerProject()
        {
            CopyFromInternal(null);
        }

        [Fact]
        public void CopyFromCrossProjectSameCollection()
        {
            CopyFromInternal(this.StdGroup.TargetXmlPair.View);
        }

        [Fact]
        public void CopyFromCrossProjectLocalSource()
        {
            CopyFromInternal(this.StdGroup.LocalBig.Xml);
        }

        [Fact]
        public void CopyFromCrossProjectCrossCollection()
        {
            CopyFromInternal(this.StdGroup.GuestXmlPair.View);
        }
    }
}
