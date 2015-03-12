// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class MakeDir_Tests
    {
        /// <summary>
        /// Make sure that attributes set on input items are forwarded to output items.
        /// </summary>
        [TestMethod]
        public void AttributeForwarding()
        {
            string temp = Path.GetTempPath();
            string dir = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A391");

            try
            {
                MakeDir t = new MakeDir();
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.Directories = new ITaskItem[]
                {
                    new TaskItem(dir)
                };
                t.Directories[0].SetMetadata("Locale", "en-GB");

                bool success = t.Execute();

                Assert.IsTrue(success);
                Assert.AreEqual(1, t.DirectoriesCreated.Length);
                Assert.AreEqual(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.IsTrue
                (
                    engine.Log.Contains
                    (
                        String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir)
                    )
                );
                Assert.AreEqual("en-GB", t.DirectoriesCreated[0].GetMetadata("Locale"));

                // Output ItemSpec should not be overwritten.
                Assert.AreEqual(dir, t.DirectoriesCreated[0].ItemSpec);
            }
            finally
            {
                Directory.Delete(dir);
            }
        }

        /// <summary>
        /// Check that if we fail to create a folder, we don't pass
        /// through the input.
        /// </summary>
        [TestMethod]
        public void SomeInputsFailToCreate()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A38e");
            string dir = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A38f");
            string invalid = "!@#$%^&*()|";
            string dir2 = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A390");

            try
            {
                FileStream fs = File.Create(file);
                fs.Close(); //we're gonna try to delete it

                MakeDir t = new MakeDir();
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.Directories = new ITaskItem[]
                {
                    new TaskItem(dir),
                    new TaskItem(file),
                    new TaskItem(invalid),
                    new TaskItem(dir2)
                };

                bool success = t.Execute();

                Assert.IsTrue(!success);
                Assert.AreEqual(2, t.DirectoriesCreated.Length);
                Assert.AreEqual(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.AreEqual(dir2, t.DirectoriesCreated[1].ItemSpec);
                Assert.IsTrue
                (
                    engine.Log.Contains
                    (
                        String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir)
                    )
                );
            }
            finally
            {
                Directory.Delete(dir);
                File.Delete(file);
                Directory.Delete(dir2);
            }
        }

        /// <summary>
        /// Creating a directory that already exists should not log anything.
        /// </summary>
        [TestMethod]
        public void CreateNewDirectory()
        {
            string temp = Path.GetTempPath();
            string dir = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A38C");

            try
            {
                MakeDir t = new MakeDir();
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.Directories = new ITaskItem[]
                {
                    new TaskItem(dir)
                };

                bool success = t.Execute();

                Assert.IsTrue(success);
                Assert.AreEqual(1, t.DirectoriesCreated.Length);
                Assert.AreEqual(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.IsTrue
                (
                    engine.Log.Contains
                    (
                        String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir)
                    )
                );

                engine.Log = "";
                success = t.Execute();

                Assert.IsTrue(success);
                // should still return directory even though it didn't need to be created
                Assert.AreEqual(1, t.DirectoriesCreated.Length);
                Assert.AreEqual(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.IsTrue
                (
                    !engine.Log.Contains
                    (
                        String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir)
                    )
                );
            }
            finally
            {
                Directory.Delete(dir);
            }
        }

        /*
        * Method:   FileAlreadyExists
        *
        * Make sure that nice message is logged if a file already exists with that name.
        */
        [TestMethod]
        public void FileAlreadyExists()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A38d");

            try
            {
                FileStream fs = File.Create(file);
                fs.Close(); //we're gonna try to delete it

                MakeDir t = new MakeDir();
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.Directories = new ITaskItem[]
                {
                    new TaskItem(file)
                };

                bool success = t.Execute();

                Assert.IsTrue(!success);
                Assert.AreEqual(0, t.DirectoriesCreated.Length);
                Assert.IsTrue(engine.Log.Contains("MSB3191"));
                Assert.IsTrue(engine.Log.Contains(file));
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}



