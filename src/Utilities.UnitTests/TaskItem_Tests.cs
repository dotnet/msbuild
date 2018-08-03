// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;
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
            to.ItemSpec.ShouldBe("Monkey.txt");
            ((string)to).ShouldBe("Monkey.txt");
            
            to.GetMetadata("Dog").ShouldBe("Bingo");
            to.GetMetadata("Cat").ShouldBe("Morris");

            // Test that item metadata are case-insensitive.
            to.SetMetadata("CaT", "");
            to.GetMetadata("Cat").ShouldBe("");

            // manipulate the item-spec a bit
            to.GetMetadata(FileUtilities.ItemSpecModifiers.Filename).ShouldBe("Monkey");
            to.GetMetadata(FileUtilities.ItemSpecModifiers.Extension).ShouldBe(".txt");
            to.GetMetadata(FileUtilities.ItemSpecModifiers.RelativeDir).ShouldBe(string.Empty);
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

            to.ItemSpec.ShouldBe("Bonobo.txt");          // ItemSpec is never overwritten
            to.GetMetadata("Sponge").ShouldBe("Bob");   // Metadata not in source are preserved.
            to.GetMetadata("Dog").ShouldBe("Harriet");  // Metadata present on destination are not overwritten.
            to.GetMetadata("Cat").ShouldBe("Mike");
            to.GetMetadata("Bird").ShouldBe("Big");
        }

        [Fact]
        public void NullITaskItem()
        {
            Should.Throw<ArgumentNullException>(() =>
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

            taskItem.MetadataNames.Count.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length);
            taskItem.MetadataCount.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length);

            // Now add one
            taskItem.SetMetadata("m", "m1");

            taskItem.MetadataNames.Count.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length + 1);
            taskItem.MetadataCount.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length + 1);
        }

        [Fact]
        public void NullITaskItemCast()
        {
            Should.Throw<ArgumentNullException>(() =>
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
            t.GetMetadata(FileUtilities.ItemSpecModifiers.Filename).ShouldBe("bamboo");
            t.GetMetadata(FileUtilities.ItemSpecModifiers.Extension).ShouldBe(".baz");
            t.GetMetadata("CUSTOM").ShouldBe("hello");
        }

        [Fact]
        public void CannotChangeModifiers()
        {
            Should.Throw<ArgumentException>(() =>
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
            Should.Throw<ArgumentException>(() =>
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

            t.MetadataCount.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length);

            t.SetMetadata("grog", "RUM");

            t.MetadataCount.ShouldBe(FileUtilities.ItemSpecModifiers.All.Length + 1);
        }


        [Fact]
        public void NonexistentRequestFullPath()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.FullPath).ShouldBe(
                Path.Combine
                (
                    Directory.GetCurrentDirectory(),
                    "Monkey.txt"
                )
            );
        }

        [Fact]
        public void NonexistentRequestRootDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.RootDir).ShouldBe(Path.GetPathRoot(from.GetMetadata(FileUtilities.ItemSpecModifiers.FullPath)));
        }

        [Fact]
        public void NonexistentRequestFilename()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.Filename).ShouldBe("Monkey");
        }

        [Fact]
        public void NonexistentRequestExtension()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.Extension).ShouldBe(".txt");
        }

        [Fact]
        public void NonexistentRequestRelativeDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.RelativeDir).Length.ShouldBe(0);
        }

        [Fact]
        public void NonexistentRequestDirectory()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = NativeMethodsShared.IsWindows ? @"c:\subdir\Monkey.txt" : "/subdir/Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.Directory).ShouldBe(NativeMethodsShared.IsWindows ? @"subdir\" : "subdir/");
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
            from.GetMetadata(FileUtilities.ItemSpecModifiers.Directory).ShouldBe(@"subdir\");
        }

        [Fact]
        public void NonexistentRequestRecursiveDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";

            from.GetMetadata(FileUtilities.ItemSpecModifiers.RecursiveDir).Length.ShouldBe(0);
        }

        [Fact]
        public void NonexistentRequestIdentity()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(FileUtilities.ItemSpecModifiers.Identity).ShouldBe("Monkey.txt");
        }

        [Fact]
        public void RequestTimeStamps()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = FileUtilities.GetTemporaryFile();

            from.GetMetadata(FileUtilities.ItemSpecModifiers.ModifiedTime).Length.ShouldBeGreaterThan(0);

            from.GetMetadata(FileUtilities.ItemSpecModifiers.CreatedTime).Length.ShouldBeGreaterThan(0);

            from.GetMetadata(FileUtilities.ItemSpecModifiers.AccessedTime).Length.ShouldBeGreaterThan(0);

            File.Delete(from.ItemSpec);

            from.GetMetadata(FileUtilities.ItemSpecModifiers.ModifiedTime).Length.ShouldBe(0);

            from.GetMetadata(FileUtilities.ItemSpecModifiers.CreatedTime).Length.ShouldBe(0);

            from.GetMetadata(FileUtilities.ItemSpecModifiers.AccessedTime).Length.ShouldBe(0);
        }

        /// <summary>
        /// Verify metadata cannot be created with null name
        /// </summary>
        [Fact]
        public void CreateNullNamedMetadata()
        {
            Should.Throw<ArgumentNullException>(() =>
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
            Should.Throw<ArgumentException>(() =>
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
            item.GetMetadata("m").ShouldBe(string.Empty);
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
            item.GetMetadata("m").ShouldBe(string.Empty);
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

                creator.Run(new[] { "a", "b" }, metadata);

                ITaskItem[] itemsInThisAppDomain = new ITaskItem[creator.CreatedTaskItems.Length];

                for (int i = 0; i < creator.CreatedTaskItems.Length; i++)
                {
                    itemsInThisAppDomain[i] = new TaskItem(creator.CreatedTaskItems[i]);

                    itemsInThisAppDomain[i].ItemSpec.ShouldBe(creator.CreatedTaskItems[i].ItemSpec);
                    itemsInThisAppDomain[i].MetadataCount.ShouldBe(creator.CreatedTaskItems[i].MetadataCount + 1);

                    foreach (string metadatum in creator.CreatedTaskItems[i].MetadataNames)
                    {
                        if (!string.Equals("OriginalItemSpec", metadatum))
                        {
                            itemsInThisAppDomain[i].GetMetadata(metadatum).ShouldBe(creator.CreatedTaskItems[i].GetMetadata(metadatum));
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
