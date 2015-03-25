// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class FindAppConfigFile_Tests
    {
        [Test]
        public void FoundInFirstInProjectDirectory()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("app.config"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("app.config", f.AppConfigFile.ItemSpec);
            Assert.AreEqual("targetpath", f.AppConfigFile.GetMetadata("TargetPath"));
        }

        [Test]
        public void FoundInSecondInProjectDirectory()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("yyy"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { new TaskItem("app.config"), new TaskItem("xxx") };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("app.config", f.AppConfigFile.ItemSpec);
            Assert.AreEqual("targetpath", f.AppConfigFile.GetMetadata("TargetPath"));
        }

        [Test]
        public void FoundInSecondBelowProjectDirectory()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("yyy"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { new TaskItem("foo\\app.config"), new TaskItem("xxx") };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(FileUtilities.FixFilePath("foo\\app.config"), f.AppConfigFile.ItemSpec);
            Assert.AreEqual("targetpath", f.AppConfigFile.GetMetadata("TargetPath"));
        }

        [Test]
        public void NotFound()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("yyy"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { new TaskItem("iii"), new TaskItem("xxx") };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual(null, f.AppConfigFile);
        }

        [Test]
        public void MatchFileNameOnlyWithAnInvalidPath()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("yyy"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { new TaskItem("|||"), new TaskItem(@"foo\\app.config"), new TaskItem(@"!@#$@$%|"), new TaskItem("uuu") };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            // Should ignore the invalid paths
            Assert.AreEqual(FileUtilities.FixFilePath(@"foo\\app.config"), f.AppConfigFile.ItemSpec);
        }

        // For historical reasons, we should return the last one in the list
        [Test]
        public void ReturnsLastOne()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            ITaskItem item1 = new TaskItem("app.config");
            item1.SetMetadata("id", "1");
            ITaskItem item2 = new TaskItem("app.config");
            item2.SetMetadata("id", "2");
            f.PrimaryList = new ITaskItem[] { item1, item2 };
            f.SecondaryList = new ITaskItem[] { };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("app.config", f.AppConfigFile.ItemSpec);
            Assert.AreEqual(item2.GetMetadata("id"), f.AppConfigFile.GetMetadata("id"));
        }
    }
}

