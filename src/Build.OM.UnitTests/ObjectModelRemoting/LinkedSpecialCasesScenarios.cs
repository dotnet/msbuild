// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Linq;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Xunit;

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
                LocalBigPath = ImmutableDisk.WriteProjectFile($"BigLocal.proj", BigProjectFile);
                TargetBigPath = ImmutableDisk.WriteProjectFile($"BigTarget.proj", BigProjectFile);
                GuestBigPath = ImmutableDisk.WriteProjectFile($"BigGuest.proj", BigProjectFile);

                Target = Remote[0];
                Guest = Remote[1];

                LocalBig = Local.LoadProjectIgnoreMissingImports(LocalBigPath);
                TargetBig = Target.LoadProjectIgnoreMissingImports(TargetBigPath);
                GuestBig = Guest.LoadProjectIgnoreMissingImports(GuestBigPath);

                TakeSnapshot();
            }

            public void ResetBeforeTests()
            {
                Clear();
                Local.Importing = true;
                {
                    var targetView = Local.GetLoadedProjects(TargetBigPath).FirstOrDefault();
                    Assert.NotNull(targetView);
                    var targetPair = new ProjectPair(targetView, TargetBig);
                    TargetXmlPair = new ProjectXmlPair(targetPair);
                }

                {
                    var guestView = Local.GetLoadedProjects(GuestBigPath).FirstOrDefault();
                    Assert.NotNull(guestView);
                    var guestPair = new ProjectPair(guestView, GuestBig);
                    GuestXmlPair = new ProjectXmlPair(guestPair);
                }
            }
        }

        public MyTestCollectionGroup StdGroup { get; }
        public LinkedSpecialCasesScenarios(MyTestCollectionGroup group)
        {
            StdGroup = group;
            group.ResetBeforeTests();
        }

        private ProjectPair GetNewInMemoryProject(string path, string content = null)
        {
            content ??= TestCollectionGroup.SampleProjectFile;
            var tempPath = StdGroup.Disk.GetAbsolutePath(path);
            var newReal = StdGroup.Target.LoadInMemoryWithSettings(content, ProjectLoadSettings.IgnoreMissingImports);
            newReal.Xml.FullPath = tempPath;
            var newView = StdGroup.Local.GetLoadedProjects(tempPath).FirstOrDefault();
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

            sourceProject ??= xmlPair.View;


            // var existingItemGroup1 = sourceProject.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1");
            var existingItemGroupList = sourceProject.AllChildren.OfType<ProjectItemGroupElement>().Where((ig) => ig.Label == "Group1").ToList();
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

            sourceProject ??= xmlPair.View;

            var existingItemGroupList = sourceProject.AllChildren.OfType<ProjectItemGroupElement>().Where((ig) => ig.Label == "Group1").ToList();
            Assert.Single(existingItemGroupList);
            var existingItemGroup = existingItemGroupList[0];
            Assert.NotNull(existingItemGroup);
            var realExistingItemGroup = ViewValidation.GetRealObject(existingItemGroup);

            var ourGroup1 = xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1");

            var newCopyFrom = xmlPair.AddNewLabeledChaildWithVerify(ObjectType.View, "newGrop", (p, l) => p.AddItemGroup());

            newCopyFrom.View.CopyFrom(existingItemGroup);
            xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1", 2);
            newCopyFrom.View.Label = "CopyFrom";
            newCopyFrom.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "CopyFrom"));
            ourGroup1.VerifySame(xmlPair.QuerySingleChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1"));
            // children are not copied.
            Assert.Empty(newCopyFrom.View.Items);
            // but attributes are (even non standard)
            // Assert.Equal("2", ProjectElementLink.GetAttributeValue(existingItemGroup, "FunnyAttribute", true));
            // Assert.Equal("2", ProjectElementLink.GetAttributeValue(newCopyFrom.View, "FunnyAttribute", true));
            newCopyFrom.VerifyNotSame(ourGroup1);


            Assert.True(xmlPair.View.HasUnsavedChanges);
            Assert.False(externalSource && sourceProject.HasUnsavedChanges);

            var newDeepCopy = xmlPair.AddNewLabeledChaildWithVerify(ObjectType.View, "newGrop", (p, l) => p.AddItemGroup());
            newDeepCopy.View.DeepCopyFrom(existingItemGroup);

            xmlPair.QueryChildrenWithValidation<ProjectItemGroupElement>((ig) => ig.Label == "Group1", 2);
            // slightly cheting but we know that the large groups should be the same, even though there are not the same object
            // note do that before changing the label.
            Assert.NotSame(realExistingItemGroup, newDeepCopy.Real);
            // TODO XmlLocation is (correctly) different for the items, need to find a way to bypass it.
            var context = new ValidationContext();
            context.ValidateLocation = delegate (ElementLocation a, ElementLocation e) { return; };

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
            CloneAndAddInternal(StdGroup.TargetXmlPair.View);
        }

        [Fact]
        public void CloneAndAddCrossProjectLocalSource()
        {
            // view gets "a real" object as argument
            CloneAndAddInternal(StdGroup.LocalBig.Xml);
        }

        [Fact]
        public void CloneAndAddCrossProjectCrossCollection()
        {
            // view gets "a view" object as argument from different project and collection (double proxy)
            CloneAndAddInternal(StdGroup.GuestXmlPair.View);
        }


        [Fact]
        public void CopyFromInnerProject()
        {
            CopyFromInternal(null);
        }

        [Fact]
        public void CopyFromCrossProjectSameCollection()
        {
            CopyFromInternal(StdGroup.TargetXmlPair.View);
        }

        [Fact]
        public void CopyFromCrossProjectLocalSource()
        {
            CopyFromInternal(StdGroup.LocalBig.Xml);
        }

        [Fact]
        public void CopyFromCrossProjectCrossCollection()
        {
            CopyFromInternal(StdGroup.GuestXmlPair.View);
        }
    }
}
