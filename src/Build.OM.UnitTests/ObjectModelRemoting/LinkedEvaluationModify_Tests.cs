// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Build.Evaluation;

    [TestClass]
    public class LinkedEvaluationModify_Tests
    {
        public class MyTestCollectionGroup : TestCollectionGroup
        {
            public MyTestCollectionGroup() : base(2, 1) { }
        }

        public TestCollectionGroup StdGroup { get; }

        private static MyTestCollectionGroup s_stdGroup;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context) => s_stdGroup = new MyTestCollectionGroup();

        [ClassCleanup]
        public static void ClassCleanup() => s_stdGroup?.Dispose();

        public LinkedEvaluationModify_Tests()
        {
            this.StdGroup = s_stdGroup;
            s_stdGroup.Clear();
        }



        [MSBuildTestMethod]
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

            Assert.AreNotEqual(proj1Path, savedPath);
            Assert.AreEqual(proj1Path, viewProj.FullPath);
            Assert.IsTrue(File.Exists(proj1Path));
            Assert.IsFalse(File.Exists(savedPath));

            var lwtBefore = new FileInfo(proj1Path).LastWriteTimeUtc;

            Assert.IsFalse(realProj.IsDirty);
            Assert.IsFalse(viewProj.IsDirty);

            viewProj.FullPath = savedPath;
            Assert.AreEqual(savedPath, realProj.FullPath);
            Assert.IsTrue(realProj.IsDirty);
            Assert.IsTrue(viewProj.IsDirty);

            viewProj.Save();

            Assert.IsTrue(realProj.IsDirty);
            Assert.IsTrue(viewProj.IsDirty);
            // it should still be dirty since it is not reevaluated.


            Assert.IsTrue(File.Exists(savedPath));

            var lwtAfter = new FileInfo(proj1Path).LastWriteTimeUtc;
            Assert.AreEqual(lwtBefore, lwtAfter);


            viewProj.ReevaluateIfNecessary();

            // now it should be not dirty anymore.
            Assert.IsFalse(realProj.IsDirty);
            Assert.IsFalse(viewProj.IsDirty);

            realProj.IsBuildEnabled = false;
            Assert.IsFalse(viewProj.IsBuildEnabled);
            Assert.IsFalse(realProj.IsBuildEnabled);
            viewProj.IsBuildEnabled = true;
            Assert.IsTrue(viewProj.IsBuildEnabled);
            Assert.IsTrue(realProj.IsBuildEnabled);

            realProj.SkipEvaluation = false;
            Assert.IsFalse(viewProj.SkipEvaluation);
            Assert.IsFalse(realProj.SkipEvaluation);
            viewProj.SkipEvaluation = true;
            Assert.IsTrue(viewProj.SkipEvaluation);
            Assert.IsTrue(realProj.SkipEvaluation);

            realProj.ThrowInsteadOfSplittingItemElement = false;
            Assert.IsFalse(viewProj.ThrowInsteadOfSplittingItemElement);
            Assert.IsFalse(realProj.ThrowInsteadOfSplittingItemElement);
            viewProj.ThrowInsteadOfSplittingItemElement = true;
            Assert.IsTrue(viewProj.ThrowInsteadOfSplittingItemElement);
            Assert.IsTrue(realProj.ThrowInsteadOfSplittingItemElement);

            // and finally just ensure that all is identical
            ViewValidation.Verify(viewProj, realProj);
        }

        [MSBuildTestMethod]
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

            // test AddItems
            // add a new files in the view, ensure it is added correctly and also the real object will immediately reflect that add as well
            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "foo.cpp"));
            var fooView = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "foo.cpp");

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooFast.cpp"));
            var fooViewFast = pair.AddSingleItemFastWithVerify(ObjectType.View, "cpp", "fooFast.cpp");

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
            var fooWithMetadataView = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "fooWithMetadata.cpp", testMedatada);

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadataFast.cpp"));
            var fooWithMetadataViewFast = pair.AddSingleItemWithVerify(ObjectType.View, "cpp", "fooWithMetadataFast.cpp", testMedatada);

            // add a new files in the real, ensure it is added correctly and also the view object will immediately reflect that add as well
            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "bar.cpp"));
            var barReal = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "bar.cpp");

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
            var barRealFast = pair.AddSingleItemFastWithVerify(ObjectType.Real, "cpp", "barFast.cpp");

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barWithMetadata.cpp"));
            var barWithMetadataReal = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "barWithMetadata.cpp", testMedatada);

            Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barWithMetadataFast.cpp"));
            var barWithMetadataRealFast = pair.AddSingleItemWithVerify(ObjectType.Real, "cpp", "barWithMetadataFast.cpp", testMedatada);


            ViewValidation.Verify(pair);

            // Test remove items.

            var validationContext = new ValidationContext(pair);
            // remove single from view
            {
                Assert.IsNotNull(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp"));
                var barWithMetadataViewFast = pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp");
                Assert.IsNotNull(barWithMetadataViewFast);

                ViewValidation.Verify(barWithMetadataViewFast, barWithMetadataRealFast, validationContext);
                Assert.ThrowsExactly<ArgumentException>(() =>
                   {
                       pair.Real.RemoveItem(barWithMetadataViewFast);
                   });

                pair.View.RemoveItem(barWithMetadataViewFast);
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadataFast.cpp"));
            }

            // remove multiple from view
            {
                Assert.IsNotNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
                var barWithMetadataView = pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadata.cpp");
                Assert.IsNotNull(barWithMetadataView);
                ViewValidation.Verify(barWithMetadataView, barWithMetadataReal, validationContext);
                var toRemoveView = new List<ProjectItem>() { barWithMetadataView, fooWithMetadataView };

                Assert.ThrowsExactly<ArgumentException>(() =>
                {
                    pair.Real.RemoveItems(toRemoveView);
                });

                pair.View.RemoveItems(toRemoveView);
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "fooWithMetadata.cpp"));
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.View, "barWithMetadata.cpp"));
            }


            // remove single from real
            {
                Assert.IsNotNull(pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp"));
                var fooWithMetadataRealFast = pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp");
                Assert.IsNotNull(fooWithMetadataRealFast);
                ViewValidation.Verify(fooWithMetadataViewFast, fooWithMetadataRealFast, validationContext);

                // Note in reality we do not guarantee that the Export provider will re-throw exactly the same exception.
                // (some exception can be hard to marshal) Current mock does in fact forward exact exception.)
                Assert.ThrowsExactly<ArgumentException>(() =>
                {
                    pair.View.RemoveItem(fooWithMetadataRealFast);
                });


                pair.Real.RemoveItem(fooWithMetadataRealFast);
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "fooWithMetadataFast.cpp"));
            }

            // remove multiple from real
            {
                Assert.IsNotNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
                var fooRealFast = pair.GetSingleItemWithVerify(ObjectType.Real, "fooFast.cpp");
                Assert.IsNotNull(fooRealFast);
                ViewValidation.Verify(fooViewFast, fooRealFast, validationContext);
                var toRemoveReal = new List<ProjectItem>() { fooRealFast, barRealFast };

                Assert.ThrowsExactly<ArgumentException>(() =>
                {
                    pair.View.RemoveItems(toRemoveReal);
                });

                pair.Real.RemoveItems(toRemoveReal);
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "fooFast.cpp"));
                Assert.IsNull(pair.GetSingleItemWithVerify(ObjectType.Real, "barFast.cpp"));
            }


            // Check metadata modify
            var fooReal = pair.GetSingleItemWithVerify(ObjectType.Real, "foo.cpp");
            ViewValidation.Verify(fooView, fooReal, validationContext);

            Assert.IsFalse(fooView.HasMetadata("xx"));
            fooView.SetMetadataValue("xx", "xxValue");
            Assert.IsTrue(fooView.HasMetadata("xx"));
            Assert.AreEqual("xxValue", fooView.GetMetadataValue("xx"));
            ViewValidation.Verify(fooView, fooReal, validationContext);


            Assert.IsFalse(fooView.RemoveMetadata("xxNone"));
            Assert.IsTrue(fooView.RemoveMetadata("xx"));
            Assert.IsFalse(fooView.HasMetadata("xx"));

            ViewValidation.Verify(fooView, fooReal, validationContext);
            // now check metadata modify via real also affect view.

            Assert.IsFalse(fooView.HasMetadata("xxReal"));
            fooReal.SetMetadataValue("xxReal", "xxRealValue");
            Assert.IsTrue(fooView.HasMetadata("xxReal"));
            Assert.AreEqual("xxRealValue", fooView.GetMetadataValue("xxReal"));
            ViewValidation.Verify(fooView, fooReal, validationContext);

            Assert.IsTrue(fooReal.RemoveMetadata("xxReal"));
            Assert.IsFalse(fooView.HasMetadata("xxReal"));

            ViewValidation.Verify(fooView, fooReal, validationContext);

            // TODO: test the boolean form (low value for linking really).

            // ItemType set.
            Assert.AreEqual("cpp", fooView.ItemType);
            fooView.ItemType = "cpp2";
            Assert.AreEqual("cpp2", fooView.ItemType);
            Assert.AreEqual("cpp2", fooReal.ItemType);
            fooReal.ItemType = "cpp3";
            Assert.AreEqual("cpp3", fooView.ItemType);
            Assert.AreEqual("cpp3", fooReal.ItemType);

            ViewValidation.Verify(fooView, fooReal, validationContext);

            // UnevaluatedInclude set

            Assert.AreEqual("foo.cpp", fooView.UnevaluatedInclude);
            fooView.UnevaluatedInclude = "fooRenamed.cpp";
            Assert.AreEqual("fooRenamed.cpp", fooView.UnevaluatedInclude);
            Assert.AreEqual("fooRenamed.cpp", fooReal.UnevaluatedInclude);

            fooReal.UnevaluatedInclude = "fooRenamedAgain.cpp";
            Assert.AreEqual("fooRenamedAgain.cpp", fooView.UnevaluatedInclude);
            Assert.AreEqual("fooRenamedAgain.cpp", fooReal.UnevaluatedInclude);
            ViewValidation.Verify(fooView, fooReal, validationContext);

            // Rename.
            fooView.Rename("fooRenamedOnceMore.cpp");
            Assert.AreEqual("fooRenamedOnceMore.cpp", fooView.UnevaluatedInclude);
            Assert.AreEqual("fooRenamedOnceMore.cpp", fooReal.UnevaluatedInclude);

            fooReal.Rename("fooRenamedLastTimeForSure.cpp");
            Assert.AreEqual("fooRenamedLastTimeForSure.cpp", fooView.UnevaluatedInclude);
            Assert.AreEqual("fooRenamedLastTimeForSure.cpp", fooReal.UnevaluatedInclude);
            ViewValidation.Verify(fooView, fooReal, validationContext);


            // and finally again verify the two projects are equivalent as a whole.
            ViewValidation.Verify(pair);
        }

        [MSBuildTestMethod]
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

            Assert.IsFalse(pair.View.GlobalProperties.ContainsKey("gp1"));
            Assert.IsFalse(pair.View.GlobalProperties.ContainsKey("Configuration"));
            // at this point Configuration is not set and gp1 is not set.
            pair.ValidatePropertyValue("gpt1", "NotFoo");

            pair.View.SetGlobalProperty("gp1", "GP1V");
            Assert.IsTrue(pair.View.GlobalProperties.ContainsKey("gp1"));
            Assert.IsTrue(pair.Real.GlobalProperties.ContainsKey("gp1"));

            // not evaluated yet.
            pair.ValidatePropertyValue("gpt1", "NotFoo");
            pair.View.ReevaluateIfNecessary();
            pair.ValidatePropertyValue("gpt1", "NotFooGP1V");


            pair.Real.SetGlobalProperty("Configuration", "Foo");
            Assert.IsTrue(pair.View.GlobalProperties.ContainsKey("Configuration"));
            pair.ValidatePropertyValue("gpt1", "NotFooGP1V");
            pair.View.ReevaluateIfNecessary();
            pair.ValidatePropertyValue("gpt1", "FooGP1V");
        }

        [MSBuildTestMethod]
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

            Assert.AreEqual("fooValue", fooView.EvaluatedValue);
            pair.Real.SetGlobalProperty("xxx", "XXX");
            Assert.AreEqual("fooValue", fooView.EvaluatedValue);
            pair.Real.ReevaluateIfNecessary();
            // note msbuild create a new property objects on reevaluation.
            Assert.AreEqual("fooValue", fooView.EvaluatedValue);
            Assert.AreEqual("fooValue", fooReal.EvaluatedValue);
            var fooRealNew = pair.Real.GetProperty("fooProp");
            var fooViewNew = pair.View.GetProperty("fooProp");
            Assert.AreNotSame(fooReal, fooRealNew);
            Assert.AreNotSame(fooView, fooViewNew);

            Assert.AreEqual("fooValueXXX", fooViewNew.EvaluatedValue);

            fooViewNew.UnevaluatedValue = "fooValueChanged$(xxx)";
            Assert.AreEqual("fooValueChanged$(xxx)", fooRealNew.UnevaluatedValue);
            // but when changing the Unevaluate via ProjectProp element it does update the live object.
            Assert.AreEqual("fooValueChangedXXX", fooViewNew.EvaluatedValue);
            Assert.AreEqual("fooValueChangedXXX", fooRealNew.EvaluatedValue);

            ViewValidation.Verify(pair);

            // note this should work even though the fooView is recycled.
            Assert.IsTrue(pair.View.RemoveProperty(fooView));
            Assert.IsNull(pair.View.GetProperty("fooProp"));
        }
    }
}
