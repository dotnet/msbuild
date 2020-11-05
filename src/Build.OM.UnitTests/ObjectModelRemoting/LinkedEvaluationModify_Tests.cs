// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Build.Evaluation;
    using Xunit;

    public class LinkedEvaluationModify_Tests : IClassFixture<LinkedEvaluationModify_Tests.MyTestCollectionGroup>
    {
        public class MyTestCollectionGroup : TestCollectionGroup
        {
            public MyTestCollectionGroup() : base(2, 1) { }
        }

        public TestCollectionGroup StdGroup { get; }
        public LinkedEvaluationModify_Tests(MyTestCollectionGroup group)
        {
            this.StdGroup = group;
            group.Clear();
        }



        [Fact]
        public void ProjectModifyRenameAndSafeAs()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];

            var proj1Path = this.StdGroup.StdProjectFiles[0];
            var realProj = pcRemote.LoadProject(proj1Path);
            pcLocal.Importing = true;
            var viewProj = pcLocal.Collection.GetLoadedProjects(proj1Path).FirstOrDefault();


            ViewValidation.Verify(viewProj, realProj);
            var savedPath = this.StdGroup.Disk.GetAbsolutePath("Saved.proj");

            Assert.NotEqual(proj1Path, savedPath);
            Assert.Equal(proj1Path, viewProj.FullPath);
            Assert.True(File.Exists(proj1Path));
            Assert.False(File.Exists(savedPath));

            var lwtBefore = new FileInfo(proj1Path).LastWriteTimeUtc;

            Assert.False(realProj.IsDirty);
            Assert.False(viewProj.IsDirty);

            viewProj.FullPath = savedPath;
            Assert.Equal(savedPath, realProj.FullPath);
            Assert.True(realProj.IsDirty);
            Assert.True(viewProj.IsDirty);

            viewProj.Save();

            Assert.True(realProj.IsDirty);
            Assert.True(viewProj.IsDirty);
            // it should still be dirty since it is not reevaluated.


            Assert.True(File.Exists(savedPath));

            var lwtAfter = new FileInfo(proj1Path).LastWriteTimeUtc;
            Assert.Equal(lwtBefore, lwtAfter);


            viewProj.ReevaluateIfNecessary();

            // now it should be not dirty anymore.
            Assert.False(realProj.IsDirty);
            Assert.False(viewProj.IsDirty);

            realProj.IsBuildEnabled = false;
            Assert.False(viewProj.IsBuildEnabled);
            Assert.False(realProj.IsBuildEnabled);
            viewProj.IsBuildEnabled = true;
            Assert.True(viewProj.IsBuildEnabled);
            Assert.True(realProj.IsBuildEnabled);

            realProj.SkipEvaluation = false;
            Assert.False(viewProj.SkipEvaluation);
            Assert.False(realProj.SkipEvaluation);
            viewProj.SkipEvaluation = true;
            Assert.True(viewProj.SkipEvaluation);
            Assert.True(realProj.SkipEvaluation);

            realProj.ThrowInsteadOfSplittingItemElement = false;
            Assert.False(viewProj.ThrowInsteadOfSplittingItemElement);
            Assert.False(realProj.ThrowInsteadOfSplittingItemElement);
            viewProj.ThrowInsteadOfSplittingItemElement = true;
            Assert.True(viewProj.ThrowInsteadOfSplittingItemElement);
            Assert.True(realProj.ThrowInsteadOfSplittingItemElement);

            // and finally just ensure that all is identical
            ViewValidation.Verify(viewProj, realProj);
        }

        [Fact]
        public void ProjectItemModify()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];

            var proj1Path = this.StdGroup.StdProjectFiles[0];
            var realProj = pcRemote.LoadProject(proj1Path);
            pcLocal.Importing = true;
            var viewProj = pcLocal.Collection.GetLoadedProjects(proj1Path).FirstOrDefault();

            ProjectPair pair = new ProjectPair(viewProj, realProj);
            ViewValidation.Verify(pair);


            List<KeyValuePair<string, string>> testMedatada = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("a", "aValue"),
                new KeyValuePair<string, string>("b", "bValue"),
            };

            /// test AddItems
            // add a new files in the view, ensure it is added correctly and also the real object will immediately reflect that add as well
            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "foo.cpp"));
            var fooView = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "foo.cpp");

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "fooFast.cpp"));
            var fooViewFast = pair.AddSingleItemFastWithVerify(ObjectType.View, "cpp", "fooFast.cpp");

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
            var fooWithMetadataView = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "fooWithMetadata.cpp", testMedatada);

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadataFast.cpp"));
            var fooWithMetadataViewFast = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "fooWithMetadataFast.cpp", testMedatada);

            // add a new files in the real, ensure it is added correctly and also the view object will immediately reflect that add as well
            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "bar.cpp"));
            var barReal = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "bar.cpp");

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
            var barRealFast = pair.AddSingleItemFastWithVerify(ObjectType.Real, "cpp", "barFast.cpp");

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "barWithMetadata.cpp"));
            var barWithMetadataReal = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "barWithMetadata.cpp", testMedatada);

            Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "barWithMetadataFast.cpp"));
            var barWithMetadataRealFast = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "barWithMetadataFast.cpp", testMedatada);


            ViewValidation.Verify(pair);

            // Test remove items.

            var validationContext = new ValidationContext(pair);
            // remove single from view
            {
                Assert.NotNull(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp"));
                var barWithMetadataViewFast = pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp");
                Assert.NotNull(barWithMetadataViewFast);

                ViewValidation.Verify(barWithMetadataViewFast, barWithMetadataRealFast, validationContext);
                Assert.Throws<ArgumentException>(() =>
                   {
                       pair.Real.RemoveItem(barWithMetadataViewFast);
                   });

                pair.View.RemoveItem(barWithMetadataViewFast);
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp"));
            }

            // remove multiple from view
            {
                Assert.NotNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
                var barWithMetadataView = pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadata.cpp");
                Assert.NotNull(barWithMetadataView);
                ViewValidation.Verify(barWithMetadataView, barWithMetadataReal, validationContext);
                var toRemoveView = new List<ProjectItem>() { barWithMetadataView, fooWithMetadataView };

                Assert.Throws<ArgumentException>(() =>
                {
                    pair.Real.RemoveItems(toRemoveView);
                });

                pair.View.RemoveItems(toRemoveView);
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadata.cpp"));
            }


            // remove single from real
            {
                Assert.NotNull(pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp"));
                var fooWithMetadataRealFast = pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp");
                Assert.NotNull(fooWithMetadataRealFast);
                ViewValidation.Verify(fooWithMetadataViewFast, fooWithMetadataRealFast, validationContext);

                // Note in reality we do not guarantee that the Export provider will re-throw exactly the same exception.
                // (some exception can be hard to marshal) Current mock does in fact forward exact exception.)
                Assert.Throws<ArgumentException>(() =>
                {
                    pair.View.RemoveItem(fooWithMetadataRealFast);
                });


                pair.Real.RemoveItem(fooWithMetadataRealFast);
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp"));
            }

            // remove multiple from real
            {
                Assert.NotNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
                var fooRealFast = pair.GetSingleItemWithVerify(ObjectType.Real, "fooFast.cpp");
                Assert.NotNull(fooRealFast);
                ViewValidation.Verify(fooViewFast, fooRealFast, validationContext);
                var toRemoveReal = new List<ProjectItem>() { fooRealFast, barRealFast};

                Assert.Throws<ArgumentException>(() =>
                {
                    pair.View.RemoveItems(toRemoveReal);
                });

                pair.Real.RemoveItems(toRemoveReal);
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "fooFast.cpp"));
                Assert.Null(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
            }


            // Check metadata modify
            var fooReal = pair.GetSingleItemWithVerify(ObjectType.Real, "foo.cpp");
            ViewValidation.Verify(fooView, fooReal, validationContext);

            Assert.False(fooView.HasMetadata("xx"));
            fooView.SetMetadataValue("xx", "xxValue");
            Assert.True(fooView.HasMetadata("xx"));
            Assert.Equal("xxValue", fooView.GetMetadataValue("xx"));
            ViewValidation.Verify(fooView, fooReal, validationContext);


            Assert.False(fooView.RemoveMetadata("xxNone"));
            Assert.True(fooView.RemoveMetadata("xx"));
            Assert.False(fooView.HasMetadata("xx"));

            ViewValidation.Verify(fooView, fooReal, validationContext);
            // now check metadata modify via real also affect view.

            Assert.False(fooView.HasMetadata("xxReal"));
            fooReal.SetMetadataValue("xxReal", "xxRealValue");
            Assert.True(fooView.HasMetadata("xxReal"));
            Assert.Equal("xxRealValue", fooView.GetMetadataValue("xxReal"));
            ViewValidation.Verify(fooView, fooReal, validationContext);

            Assert.True(fooReal.RemoveMetadata("xxReal"));
            Assert.False(fooView.HasMetadata("xxReal"));

            ViewValidation.Verify(fooView, fooReal, validationContext);

            // TODO: test the boolean form (low value for linking really).

            // ItemType set.
            Assert.Equal("cpp", fooView.ItemType);
            fooView.ItemType = "cpp2";
            Assert.Equal("cpp2", fooView.ItemType);
            Assert.Equal("cpp2", fooReal.ItemType);
            fooReal.ItemType = "cpp3";
            Assert.Equal("cpp3", fooView.ItemType);
            Assert.Equal("cpp3", fooReal.ItemType);

            ViewValidation.Verify(fooView, fooReal, validationContext);

            // UnevaluatedInclude set

            Assert.Equal("foo.cpp", fooView.UnevaluatedInclude);
            fooView.UnevaluatedInclude = "fooRenamed.cpp";
            Assert.Equal("fooRenamed.cpp", fooView.UnevaluatedInclude);
            Assert.Equal("fooRenamed.cpp", fooReal.UnevaluatedInclude);

            fooReal.UnevaluatedInclude = "fooRenamedAgain.cpp";
            Assert.Equal("fooRenamedAgain.cpp", fooView.UnevaluatedInclude);
            Assert.Equal("fooRenamedAgain.cpp", fooReal.UnevaluatedInclude);
            ViewValidation.Verify(fooView, fooReal, validationContext);

            // Rename.
            fooView.Rename("fooRenamedOnceMore.cpp");
            Assert.Equal("fooRenamedOnceMore.cpp", fooView.UnevaluatedInclude);
            Assert.Equal("fooRenamedOnceMore.cpp", fooReal.UnevaluatedInclude);

            fooReal.Rename("fooRenamedLastTimeForSure.cpp");
            Assert.Equal("fooRenamedLastTimeForSure.cpp", fooView.UnevaluatedInclude);
            Assert.Equal("fooRenamedLastTimeForSure.cpp", fooReal.UnevaluatedInclude);
            ViewValidation.Verify(fooView, fooReal, validationContext);


            // and finally again verify the two projects are equivalent as a whole.
            ViewValidation.Verify(pair);
        }

        [Fact]
        public void ProjectGlobalPropertyModify()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];

            var proj1Path = this.StdGroup.StdProjectFiles[0];
            var realProj = pcRemote.LoadProject(proj1Path);
            pcLocal.Importing = true;
            var viewProj = pcLocal.Collection.GetLoadedProjects(proj1Path).FirstOrDefault();

            ProjectPair pair = new ProjectPair(viewProj, realProj);
            ViewValidation.Verify(pair);

            Assert.False(pair.View.GlobalProperties.ContainsKey("gp1"));
            Assert.False(pair.View.GlobalProperties.ContainsKey("Configuration"));
            // at this point Configuration is not set and gp1 is not set.
            pair.ValidatePropertyValue("gpt1", "NotFoo");

            pair.View.SetGlobalProperty("gp1", "GP1V");
            Assert.True(pair.View.GlobalProperties.ContainsKey("gp1"));
            Assert.True(pair.Real.GlobalProperties.ContainsKey("gp1"));

            // not evaluated yet.
            pair.ValidatePropertyValue("gpt1", "NotFoo");
            pair.View.ReevaluateIfNecessary();
            pair.ValidatePropertyValue("gpt1", "NotFooGP1V");


            pair.Real.SetGlobalProperty("Configuration", "Foo");
            Assert.True(pair.View.GlobalProperties.ContainsKey("Configuration"));
            pair.ValidatePropertyValue("gpt1", "NotFooGP1V");
            pair.View.ReevaluateIfNecessary();
            pair.ValidatePropertyValue("gpt1", "FooGP1V");
        }

        [Fact]
        public void ProjectPropertyModify()
        {
            var pcLocal = this.StdGroup.Local;
            var pcRemote = this.StdGroup.Remote[0];

            var proj1Path = this.StdGroup.StdProjectFiles[0];
            var realProj = pcRemote.LoadProject(proj1Path);
            pcLocal.Importing = true;
            var viewProj = pcLocal.Collection.GetLoadedProjects(proj1Path).FirstOrDefault();

            ProjectPair pair = new ProjectPair(viewProj, realProj);
            ViewValidation.Verify(pair);

            pair.ValidatePropertyValue("fooProp", string.Empty);

            var fooView = pair.SetPropertyWithVerify(ObjectType.View, "fooProp", "fooValue$(xxx)");
            var fooReal = pair.Real.GetProperty("fooProp");

            Assert.Equal("fooValue", fooView.EvaluatedValue);
            pair.Real.SetGlobalProperty("xxx", "XXX");
            Assert.Equal("fooValue", fooView.EvaluatedValue);
            pair.Real.ReevaluateIfNecessary();
            // note msbuild create a new property objects on reevaluation.
            Assert.Equal("fooValue", fooView.EvaluatedValue);
            Assert.Equal("fooValue", fooReal.EvaluatedValue);
            var fooRealNew = pair.Real.GetProperty("fooProp");
            var fooViewNew = pair.View.GetProperty("fooProp");
            Assert.NotSame(fooReal, fooRealNew);
            Assert.NotSame(fooView, fooViewNew);

            Assert.Equal("fooValueXXX", fooViewNew.EvaluatedValue);

            fooViewNew.UnevaluatedValue = "fooValueChanged$(xxx)";
            Assert.Equal("fooValueChanged$(xxx)", fooRealNew.UnevaluatedValue);
            // but when changing the Unevaluate via ProjectProp element it does update the live object.
            Assert.Equal("fooValueChangedXXX", fooViewNew.EvaluatedValue);
            Assert.Equal("fooValueChangedXXX", fooRealNew.EvaluatedValue);

            ViewValidation.Verify(pair);

            // note this should work even though the fooView is recycled.
            Assert.True(pair.View.RemoveProperty(fooView));
            Assert.Null(pair.View.GetProperty("fooProp"));
        }
    }
}
