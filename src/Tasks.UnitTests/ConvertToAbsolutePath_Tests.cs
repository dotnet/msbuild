// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class ConvertToAbsolutePath_Tests
    {
        /// <summary>
        /// Passing in a relative path (expecting an absolute back)
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void RelativePath()
        {
            string fileName = ObjectModelHelpers.CreateFileInTempProjectDirectory("file.temp", "foo");
            FileInfo testFile = new FileInfo(fileName);

            ConvertToAbsolutePath t = new ConvertToAbsolutePath();
            t.BuildEngine = new MockEngine();

            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);
                t.Paths = new ITaskItem[] { new TaskItem(@"file.temp") };
                Assert.IsTrue(t.Execute()); // "success"
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }

            Assert.ContainsSingle(t.AbsolutePaths);
            Assert.EndsWith(testFile.FullName, t.AbsolutePaths[0].ItemSpec);

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Passing in a relative path (expecting an absolute back)
        /// </summary>
        [MSBuildTestMethod]
        [TestCategory("netcore-osx-failing")]
        [TestCategory("netcore-linux-failing")]
        public void RelativePathWithEscaping()
        {
            string fileName = ObjectModelHelpers.CreateFileInTempProjectDirectory("file%3A.temp", "foo");
            FileInfo testFile = new FileInfo(fileName);

            ConvertToAbsolutePath t = new ConvertToAbsolutePath();
            t.BuildEngine = new MockEngine();

            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);
                t.Paths = new ITaskItem[] { new TaskItem(@"file%253A.temp") };
                Assert.IsTrue(t.Execute()); // "success"
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }

            Assert.ContainsSingle(t.AbsolutePaths);
            Assert.EndsWith(testFile.FullName, t.AbsolutePaths[0].ItemSpec);

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Passing in a absolute path (expecting an absolute back)
        /// </summary>
        [MSBuildTestMethod]
        public void AbsolutePath()
        {
            string fileName = ObjectModelHelpers.CreateFileInTempProjectDirectory("file.temp", "foo");
            FileInfo testFile = new FileInfo(fileName);

            ConvertToAbsolutePath t = new ConvertToAbsolutePath();
            t.BuildEngine = new MockEngine();

            string currentDirectory = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(ObjectModelHelpers.TempProjectDir);
                t.Paths = new ITaskItem[] { new TaskItem(fileName) };
                Assert.IsTrue(t.Execute()); // "success"
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }

            Assert.ContainsSingle(t.AbsolutePaths);
            Assert.AreEqual(testFile.FullName, t.AbsolutePaths[0].ItemSpec);

            ObjectModelHelpers.DeleteTempProjectDirectory();
        }

        /// <summary>
        /// Passing in a relative path that doesn't exist (expecting success)
        /// </summary>
        [MSBuildTestMethod]
        public void FakeFile()
        {
            ConvertToAbsolutePath t = new ConvertToAbsolutePath();
            t.BuildEngine = new MockEngine();

            t.Paths = new ITaskItem[] { new TaskItem("RandomFileThatDoesntExist.txt") };

            Assert.IsTrue(t.Execute()); // "success"

            Assert.ContainsSingle(t.AbsolutePaths);
        }
    }
}
