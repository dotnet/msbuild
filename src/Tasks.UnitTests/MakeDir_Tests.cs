// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class MakeDir_Tests
    {
        /// <summary>
        /// Make sure that attributes set on input items are forwarded to output items.
        /// </summary>
        [Fact]
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

                Assert.True(success);
                Assert.Single(t.DirectoriesCreated);
                Assert.Equal(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.Contains(
                    String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir),
                    engine.Log
                );
                Assert.Equal("en-GB", t.DirectoriesCreated[0].GetMetadata("Locale"));

                // Output ItemSpec should not be overwritten.
                Assert.Equal(dir, t.DirectoriesCreated[0].ItemSpec);
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(dir);
            }
        }

        /// <summary>
        /// Check that if we fail to create a folder, we don't pass
        /// through the input.
        /// </summary>
        [Fact]
        [PlatformSpecific(TestPlatforms.Windows)] // "Under Unix all filenames are valid and this test is not useful"
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
                fs.Dispose(); //we're gonna try to delete it

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

                if (NativeMethodsShared.IsWindows)
                {
                    Assert.False(success);
                    Assert.Equal(2, t.DirectoriesCreated.Length);
                    Assert.Equal(dir2, t.DirectoriesCreated[1].ItemSpec);
                }
                else
                {
                    // Since Unix pretty much does not have invalid characters,
                    // the invalid name is not really invalid
                    Assert.True(success);
                    Assert.Equal(3, t.DirectoriesCreated.Length);
                    Assert.Equal(dir2, t.DirectoriesCreated[2].ItemSpec);
                }

                Assert.Equal(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.Contains
                (
                    String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir),
                    engine.Log
                );
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(dir);
                File.Delete(file);
                if (!NativeMethodsShared.IsWindows)
                {
                    File.Delete(invalid);
                }
                FileUtilities.DeleteWithoutTrailingBackslash(dir2);
            }
        }

        /// <summary>
        /// Creating a directory that already exists should not log anything.
        /// </summary>
        [Fact]
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

                Assert.True(success);
                Assert.Single(t.DirectoriesCreated);
                Assert.Equal(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.Contains(
                    String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir),
                    engine.Log
                );

                engine.Log = "";
                success = t.Execute();

                Assert.True(success);
                // should still return directory even though it didn't need to be created
                Assert.Single(t.DirectoriesCreated);
                Assert.Equal(dir, t.DirectoriesCreated[0].ItemSpec);
                Assert.DoesNotContain(
                    String.Format(AssemblyResources.GetString("MakeDir.Comment"), dir),
                    engine.Log);
            }
            finally
            {
                FileUtilities.DeleteWithoutTrailingBackslash(dir);
            }
        }

        /*
        * Method:   FileAlreadyExists
        *
        * Make sure that nice message is logged if a file already exists with that name.
        */
        [Fact]
        public void FileAlreadyExists()
        {
            string temp = Path.GetTempPath();
            string file = Path.Combine(temp, "2A333ED756AF4dc392E728D0F864A38d");

            try
            {
                FileStream fs = File.Create(file);
                fs.Dispose(); //we're gonna try to delete it

                MakeDir t = new MakeDir();
                MockEngine engine = new MockEngine();
                t.BuildEngine = engine;

                t.Directories = new ITaskItem[]
                {
                    new TaskItem(file)
                };

                bool success = t.Execute();

                Assert.False(success);
                Assert.Empty(t.DirectoriesCreated);
                Assert.Contains("MSB3191", engine.Log);
                Assert.Contains(file, engine.Log);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}



