// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using Xunit;

#pragma warning disable 0219

namespace Microsoft.Build.UnitTests
{
    public class TaskItemTests
    {
        // Make sure a TaskItem can be constructed using an ITaskItem
        [Fact]
        public void ConstructWithITaskItem()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.SetMetadata("Dog", "Bingo");
            from.SetMetadata("Cat", "Morris");

            TaskItem to = new TaskItem((ITaskItem)from);
            Assert.Equal("Monkey.txt", to.ItemSpec);
            Assert.Equal("Monkey.txt", (string)to);
            Assert.Equal("Bingo", to.GetMetadata("Dog"));
            Assert.Equal("Morris", to.GetMetadata("Cat"));

            // Test that item metadata are case-insensitive.
            to.SetMetadata("CaT", "");
            Assert.Equal("", to.GetMetadata("Cat"));

            // manipulate the item-spec a bit
            Assert.Equal("Monkey", to.GetMetadata(FileUtilities.ItemSpecModifiers.Filename));
            Assert.Equal(".txt", to.GetMetadata(FileUtilities.ItemSpecModifiers.Extension));
            Assert.Equal(String.Empty, to.GetMetadata(FileUtilities.ItemSpecModifiers.RelativeDir));
        }

        // Make sure metadata can be cloned from an existing ITaskItem
        [Fact]
        public void CopyMetadataFromITaskItem()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.SetMetadata("Dog", "Bingo");
            from.SetMetadata("Cat", "Morris");
            from.SetMetadata("Bird", "Big");

            TaskItem to = new TaskItem();
            to.ItemSpec = "Bonobo.txt";
            to.SetMetadata("Sponge", "Bob");
            to.SetMetadata("Dog", "Harriet");
            to.SetMetadata("Cat", "Mike");
            from.CopyMetadataTo(to);

            Assert.Equal("Bonobo.txt", to.ItemSpec);          // ItemSpec is never overwritten
            Assert.Equal("Bob", to.GetMetadata("Sponge"));   // Metadata not in source are preserved.
            Assert.Equal("Harriet", to.GetMetadata("Dog"));  // Metadata present on destination are not overwritten.
            Assert.Equal("Mike", to.GetMetadata("Cat"));
            Assert.Equal("Big", to.GetMetadata("Bird"));
        }

        [Fact]
        public void NullITaskItem()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ITaskItem item = null;
                TaskItem taskItem = new TaskItem(item);

                // no NullReferenceException
            }
           );
        }
        /// <summary>
        /// Even without any custom metadata metadatanames should
        /// return the built in metadata
        /// </summary>
        [Fact]
        public void MetadataNamesNoCustomMetadata()
        {
            TaskItem taskItem = new TaskItem("x");

            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length, taskItem.MetadataNames.Count);
            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length, taskItem.MetadataCount);

            // Now add one
            taskItem.SetMetadata("m", "m1");

            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length + 1, taskItem.MetadataNames.Count);
            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length + 1, taskItem.MetadataCount);
        }

        [Fact]
        public void NullITaskItemCast()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                TaskItem item = null;
                string result = (string)item;

                // no NullReferenceException
            }
           );
        }
        [Fact]
        public void ConstructFromDictionary()
        {
            Hashtable h = new Hashtable();
            h[FileUtilities.ItemSpecModifiers.Filename] = "foo";
            h[FileUtilities.ItemSpecModifiers.Extension] = "bar";
            h["custom"] = "hello";

            TaskItem t = new TaskItem("bamboo.baz", h);

            // item-spec modifiers were not overridden by dictionary passed to constructor
            Assert.Equal("bamboo", t.GetMetadata(FileUtilities.ItemSpecModifiers.Filename));
            Assert.Equal(".baz", t.GetMetadata(FileUtilities.ItemSpecModifiers.Extension));
            Assert.Equal("hello", t.GetMetadata("CUSTOM"));
        }

        [Fact]
        public void CannotChangeModifiers()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                TaskItem t = new TaskItem("foo");

                try
                {
                    t.SetMetadata(FileUtilities.ItemSpecModifiers.FullPath, "bazbaz");
                }
                catch (Exception e)
                {
                    // so I can see the exception message in NUnit's "Standard Out" window
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
           );
        }

        [Fact]
        public void CannotRemoveModifiers()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                TaskItem t = new TaskItem("foor");

                try
                {
                    t.RemoveMetadata(FileUtilities.ItemSpecModifiers.RootDir);
                }
                catch (Exception e)
                {
                    // so I can see the exception message in NUnit's "Standard Out" window
                    Console.WriteLine(e.Message);
                    throw;
                }
            }
           );
        }
        [Fact]
        public void CheckMetadataCount()
        {
            TaskItem t = new TaskItem("foo");

            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length, t.MetadataCount);

            t.SetMetadata("grog", "RUM");

            Assert.Equal(FileUtilities.ItemSpecModifiers.All.Length + 1, t.MetadataCount);
        }


        [Fact]
        public void NonexistentRequestFullPath()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(
                Path.Combine
                (
                    Directory.GetCurrentDirectory(),
                    "Monkey.txt"
                ),
                from.GetMetadata(FileUtilities.ItemSpecModifiers.FullPath)
            );
        }

        [Fact]
        public void NonexistentRequestRootDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(
                Path.GetPathRoot
                (
                    from.GetMetadata(FileUtilities.ItemSpecModifiers.FullPath)
                ),
                from.GetMetadata(FileUtilities.ItemSpecModifiers.RootDir)
            );
        }

        [Fact]
        public void NonexistentRequestFilename()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(
                "Monkey",
                from.GetMetadata(FileUtilities.ItemSpecModifiers.Filename)
            );
        }

        [Fact]
        public void NonexistentRequestExtension()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(
                ".txt",
                from.GetMetadata(FileUtilities.ItemSpecModifiers.Extension)
            );
        }

        [Fact]
        public void NonexistentRequestRelativeDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(0, from.GetMetadata(FileUtilities.ItemSpecModifiers.RelativeDir).Length);
        }

        [Fact]
        public void NonexistentRequestDirectory()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = NativeMethodsShared.IsWindows ? @"c:\subdir\Monkey.txt" : "/subdir/Monkey.txt";
            Assert.Equal(
                NativeMethodsShared.IsWindows ? @"subdir\" : "subdir/",
                from.GetMetadata(FileUtilities.ItemSpecModifiers.Directory));
        }

        [Fact]
        public void NonexistentRequestDirectoryUNC()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                return; // "UNC is not implemented except under Windows"
            }

            TaskItem from = new TaskItem();
            from.ItemSpec = @"\\local\share\subdir\Monkey.txt";
            Assert.Equal(
                @"subdir\",
                from.GetMetadata(FileUtilities.ItemSpecModifiers.Directory)
            );
        }

        [Fact]
        public void NonexistentRequestRecursiveDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";

            Assert.Equal(0, from.GetMetadata(FileUtilities.ItemSpecModifiers.RecursiveDir).Length);
        }

        [Fact]
        public void NonexistentRequestIdentity()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            Assert.Equal(
                "Monkey.txt",
                from.GetMetadata(FileUtilities.ItemSpecModifiers.Identity)
            );
        }

        [Fact]
        public void RequestTimeStamps()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = FileUtilities.GetTemporaryFile();

            Assert.True(
                from.GetMetadata(FileUtilities.ItemSpecModifiers.ModifiedTime).Length > 0
            );

            Assert.True(
                from.GetMetadata(FileUtilities.ItemSpecModifiers.CreatedTime).Length > 0
            );

            Assert.True(
                from.GetMetadata(FileUtilities.ItemSpecModifiers.AccessedTime).Length > 0
            );

            File.Delete(from.ItemSpec);

            Assert.Equal(0, from.GetMetadata(FileUtilities.ItemSpecModifiers.ModifiedTime).Length);

            Assert.Equal(0, from.GetMetadata(FileUtilities.ItemSpecModifiers.CreatedTime).Length);

            Assert.Equal(0, from.GetMetadata(FileUtilities.ItemSpecModifiers.AccessedTime).Length);
        }

        /// <summary>
        /// Verify metadata cannot be created with null name
        /// </summary>
        [Fact]
        public void CreateNullNamedMetadata()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                TaskItem item = new TaskItem("foo");
                item.SetMetadata(null, "x");
            }
           );
        }
        /// <summary>
        /// Verify metadata cannot be created with empty name
        /// </summary>
        [Fact]
        public void CreateEmptyNamedMetadata()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                TaskItem item = new TaskItem("foo");
                item.SetMetadata("", "x");
            }
           );
        }
        /// <summary>
        /// Create a TaskItem with a null metadata value -- this is allowed, but 
        /// internally converted to the empty string. 
        /// </summary>
        [Fact]
        public void CreateTaskItemWithNullMetadata()
        {
            IDictionary<string, string> metadata = new Dictionary<string, string>();
            metadata.Add("m", null);

            TaskItem item = new TaskItem("bar", (IDictionary)metadata);
            Assert.Equal(String.Empty, item.GetMetadata("m"));
        }

        /// <summary>
        /// Set metadata value to null value -- this is allowed, but 
        /// internally converted to the empty string. 
        /// </summary>
        [Fact]
        public void SetNullMetadataValue()
        {
            TaskItem item = new TaskItem("bar");
            item.SetMetadata("m", null);
            Assert.Equal(String.Empty, item.GetMetadata("m"));
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Test that task items can be successfully constructed based on a task item from another appdomain.  
        /// </summary>
        [Fact]
        [Trait("Category", "mono-osx-failing")]
        [Trait("Category", "mono-windows-failing")]
        public void RemoteTaskItem()
        {
            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain
                            (
                                "generateResourceAppDomain",
                                null,
                                AppDomain.CurrentDomain.SetupInformation
                            );

                object obj = appDomain.CreateInstanceFromAndUnwrap
                   (
                       typeof(TaskItemCreator).Module.FullyQualifiedName,
                       typeof(TaskItemCreator).FullName
                   );

                TaskItemCreator creator = (TaskItemCreator)obj;

                IDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                metadata.Add("c", "C");
                metadata.Add("d", "D");

                creator.Run(new string[] { "a", "b" }, metadata);

                ITaskItem[] itemsInThisAppDomain = new ITaskItem[creator.CreatedTaskItems.Length];

                for (int i = 0; i < creator.CreatedTaskItems.Length; i++)
                {
                    itemsInThisAppDomain[i] = new TaskItem(creator.CreatedTaskItems[i]);

                    Assert.Equal(creator.CreatedTaskItems[i].ItemSpec, itemsInThisAppDomain[i].ItemSpec);
                    Assert.Equal(creator.CreatedTaskItems[i].MetadataCount + 1, itemsInThisAppDomain[i].MetadataCount);

                    foreach (string metadatum in creator.CreatedTaskItems[i].MetadataNames)
                    {
                        if (!String.Equals("OriginalItemSpec", metadatum))
                        {
                            Assert.Equal(creator.CreatedTaskItems[i].GetMetadata(metadatum), itemsInThisAppDomain[i].GetMetadata(metadatum));
                        }
                    }
                }
            }
            finally
            {
                if (appDomain != null)
                {
                    AppDomain.Unload(appDomain);
                }
            }
        }

        /// <summary>
        /// Miniature class to be remoted to another appdomain that just creates some TaskItems and makes them available for returning. 
        /// </summary>
        private sealed class TaskItemCreator
#if FEATURE_APPDOMAIN
                : MarshalByRefObject
#endif
        {
            /// <summary>
            /// Task items that will be consumed by the other appdomain
            /// </summary>
            public ITaskItem[] CreatedTaskItems
            {
                get;
                private set;
            }

            /// <summary>
            /// Creates task items 
            /// </summary>
            public void Run(string[] includes, IDictionary<string, string> metadataToAdd)
            {
                ErrorUtilities.VerifyThrowArgumentNull(includes, "includes");

                CreatedTaskItems = new TaskItem[includes.Length];

                for (int i = 0; i < includes.Length; i++)
                {
                    CreatedTaskItems[i] = new TaskItem(includes[i], (IDictionary)metadataToAdd);
                }
            }
        }
#endif
    }
}
