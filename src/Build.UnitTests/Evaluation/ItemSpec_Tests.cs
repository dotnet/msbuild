// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using ProjectInstanceExpander =
    Microsoft.Build.Evaluation.Expander<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;
using ProjectInstanceItemSpec =
    Microsoft.Build.Evaluation.ItemSpec<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;


#nullable disable

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    [TestClass]
    public class ItemSpec_Tests
    {
        [MSBuildTestMethod]
        public void EachFragmentTypeShouldContributeToItemSpecGlob()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.IsTrue(itemSpecGlob.IsMatch("a"));
            Assert.IsTrue(itemSpecGlob.IsMatch("bar"));
            Assert.IsTrue(itemSpecGlob.IsMatch("car"));
            Assert.IsTrue(itemSpecGlob.IsMatch("d"));
            Assert.IsTrue(itemSpecGlob.IsMatch("e"));
        }

        [MSBuildTestMethod]
        public void AbsolutePathsShouldMatch()
        {
            var absoluteRootPath = NativeMethodsShared.IsWindows
                ? @"c:\a\b"
                : "/a/b";

            var projectFile = Path.Combine(absoluteRootPath, "build.proj");
            var absoluteSpec = Path.Combine(absoluteRootPath, "s.cs");

            var itemSpecFromAbsolute = CreateItemSpecFrom(absoluteSpec, CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));
            var itemSpecFromRelative = CreateItemSpecFrom("s.cs", CreateExpander(new Dictionary<string, string[]>()), new MockElementLocation(projectFile));

            itemSpecFromRelative.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromRelative.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();

            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch("s.cs").ShouldBeTrue();
            itemSpecFromAbsolute.ToMSBuildGlob().IsMatch(absoluteSpec).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> { { "foo", new[] { "d", "e" } } }));

            int matches;
            // cause partial Lazy state to initialize in the ItemExpressionFragment
            itemSpec.FragmentsMatchingItem("e", out matches);

            Assert.AreEqual(1, matches);

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.IsTrue(itemSpecGlob.IsMatch("a"));
            Assert.IsTrue(itemSpecGlob.IsMatch("bar"));
            Assert.IsTrue(itemSpecGlob.IsMatch("car"));
            Assert.IsTrue(itemSpecGlob.IsMatch("d"));
            Assert.IsTrue(itemSpecGlob.IsMatch("e"));
        }

        private ProjectInstanceItemSpec CreateItemSpecFrom(string itemSpec, ProjectInstanceExpander expander, IElementLocation location = null)
        {
            location ??= MockElementLocation.Instance;

            return new ProjectInstanceItemSpec(itemSpec, expander, location, Path.GetDirectoryName(location.File));
        }

        private ProjectInstanceExpander CreateExpander(Dictionary<string, string[]> items)
        {
            var itemDictionary = ToItemDictionary(items);

            return new ProjectInstanceExpander(
                new PropertyDictionary<ProjectPropertyInstance>(),
                itemDictionary,
                (IFileSystem)FileSystems.Default,
                new TestLoggingContext(null!, new BuildEventContext(1, 2, 3, 4)));
        }

        private static ItemDictionary<ProjectItemInstance> ToItemDictionary(Dictionary<string, string[]> itemTypes)
        {
            var itemDictionary = new ItemDictionary<ProjectItemInstance>();

            var dummyProject = ProjectHelpers.CreateEmptyProjectInstance();

            foreach (var itemType in itemTypes)
            {
                foreach (var item in itemType.Value)
                {
                    itemDictionary.Add(new ProjectItemInstance(dummyProject, itemType.Key, item, dummyProject.FullPath));
                }
            }

            return itemDictionary;
        }
    }
}
