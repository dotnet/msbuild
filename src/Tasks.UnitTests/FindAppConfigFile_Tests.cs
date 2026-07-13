// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class FindAppConfigFile_Tests
    {
        [MSBuildTestMethod]
        public void FoundInFirstInProjectDirectory()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("app.config"), new TaskItem("xxx") };
            f.SecondaryList = System.Array.Empty<ITaskItem>();
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("app.config", f.AppConfigFile.ItemSpec);
            Assert.AreEqual("targetpath", f.AppConfigFile.GetMetadata("TargetPath"));
        }

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void NotFound()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            f.PrimaryList = new ITaskItem[] { new TaskItem("yyy"), new TaskItem("xxx") };
            f.SecondaryList = new ITaskItem[] { new TaskItem("iii"), new TaskItem("xxx") };
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.IsNull(f.AppConfigFile);
        }

        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void ReturnsLastOne()
        {
            FindAppConfigFile f = new FindAppConfigFile();
            f.BuildEngine = new MockEngine();
            ITaskItem item1 = new TaskItem("app.config");
            item1.SetMetadata("id", "1");
            ITaskItem item2 = new TaskItem("app.config");
            item2.SetMetadata("id", "2");
            f.PrimaryList = new ITaskItem[] { item1, item2 };
            f.SecondaryList = System.Array.Empty<ITaskItem>();
            f.TargetPath = "targetpath";
            Assert.IsTrue(f.Execute());
            Assert.AreEqual("app.config", f.AppConfigFile.ItemSpec);
            Assert.AreEqual(item2.GetMetadata("id"), f.AppConfigFile.GetMetadata("id"));
        }
    }
}
