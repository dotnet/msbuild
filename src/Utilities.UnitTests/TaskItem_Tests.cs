// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Shouldly;

#pragma warning disable 0219

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class TaskItemTests
    {
        // Make sure a TaskItem can be constructed using an ITaskItem
        [MSBuildTestMethod]
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
            to.GetMetadata(ItemSpecModifiers.Filename).ShouldBe("Monkey");
            to.GetMetadata(ItemSpecModifiers.Extension).ShouldBe(".txt");
            to.GetMetadata(ItemSpecModifiers.RelativeDir).ShouldBe(string.Empty);
        }

        // Make sure metadata can be cloned from an existing ITaskItem
        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        public void NullITaskItem()
        {
            Should.Throw<ArgumentNullException>(() =>
            {
                ITaskItem item = null;
                TaskItem taskItem = new TaskItem(item);

                // no NullReferenceException
            });
        }

        [MSBuildTestMethod]
        public void MetadataNamesAndCount()
        {
            TaskItem taskItem = new TaskItem("x");

            // Without custom metadata, should return the built in metadata
            taskItem.MetadataNames.Cast<string>().ShouldBeSetEquivalentTo(ItemSpecModifiers.All);
            taskItem.MetadataCount.ShouldBe(ItemSpecModifiers.All.Length);

            // Now add one
            taskItem.SetMetadata("m", "m1");

            taskItem.MetadataNames.Cast<string>().ShouldBeSetEquivalentTo(ItemSpecModifiers.All.Concat(new[] { "m" }));
            taskItem.MetadataCount.ShouldBe(ItemSpecModifiers.All.Length + 1);
        }

        [MSBuildTestMethod]
        public void NullITaskItemCast()
        {
            Should.Throw<ArgumentNullException>(() =>
            {
                TaskItem item = null;
                string result = (string)item;

                // no NullReferenceException
            });
        }
        [MSBuildTestMethod]
        public void ConstructFromDictionary()
        {
            Hashtable h = new Hashtable();
            h[ItemSpecModifiers.Filename] = "foo";
            h[ItemSpecModifiers.Extension] = "bar";
            h["custom"] = "hello";

            TaskItem t = new TaskItem("bamboo.baz", h);

            // item-spec modifiers were not overridden by dictionary passed to constructor
            t.GetMetadata(ItemSpecModifiers.Filename).ShouldBe("bamboo");
            t.GetMetadata(ItemSpecModifiers.Extension).ShouldBe(".baz");
            t.GetMetadata("CUSTOM").ShouldBe("hello");
        }

        [MSBuildTestMethod]
        public void CannotChangeModifiers()
        {
            Should.Throw<ArgumentException>(() =>
            {
                TaskItem t = new TaskItem("foo");

                try
                {
                    t.SetMetadata(ItemSpecModifiers.FullPath, "bazbaz");
                }
                catch (Exception e)
                {
                    // so I can see the exception message in NUnit's "Standard Out" window
                    Console.WriteLine(e.Message);
                    throw;
                }
            });
        }

        [MSBuildTestMethod]
        public void CannotRemoveModifiers()
        {
            Should.Throw<ArgumentException>(() =>
            {
                TaskItem t = new TaskItem("foor");

                try
                {
                    t.RemoveMetadata(ItemSpecModifiers.RootDir);
                }
                catch (Exception e)
                {
                    // so I can see the exception message in NUnit's "Standard Out" window
                    Console.WriteLine(e.Message);
                    throw;
                }
            });
        }
        [MSBuildTestMethod]
        public void CheckMetadataCount()
        {
            TaskItem t = new TaskItem("foo");

            t.MetadataCount.ShouldBe(ItemSpecModifiers.All.Length);

            t.SetMetadata("grog", "RUM");

            t.MetadataCount.ShouldBe(ItemSpecModifiers.All.Length + 1);
        }

        [MSBuildTestMethod]
        public void NonexistentRequestFullPath()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.FullPath).ShouldBe(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Monkey.txt"));
        }

        [MSBuildTestMethod]
        public void NonexistentRequestRootDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.RootDir).ShouldBe(Path.GetPathRoot(from.GetMetadata(ItemSpecModifiers.FullPath)));
        }

        [MSBuildTestMethod]
        public void NonexistentRequestFilename()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.Filename).ShouldBe("Monkey");
        }

        [MSBuildTestMethod]
        public void NonexistentRequestExtension()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.Extension).ShouldBe(".txt");
        }

        [MSBuildTestMethod]
        public void NonexistentRequestRelativeDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.RelativeDir).Length.ShouldBe(0);
        }

        [MSBuildTestMethod]
        public void NonexistentRequestDirectory()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = NativeMethodsShared.IsWindows ? @"c:\subdir\Monkey.txt" : "/subdir/Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.Directory).ShouldBe(NativeMethodsShared.IsWindows ? @"subdir\" : "subdir/");
        }

        [WindowsOnlyFact("UNC is not implemented except under Windows.")]
        public void NonexistentRequestDirectoryUNC()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = @"\\local\share\subdir\Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.Directory).ShouldBe(@"subdir\");
        }

        [MSBuildTestMethod]
        public void NonexistentRequestRecursiveDir()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";

            from.GetMetadata(ItemSpecModifiers.RecursiveDir).Length.ShouldBe(0);
        }

        [MSBuildTestMethod]
        public void NonexistentRequestIdentity()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = "Monkey.txt";
            from.GetMetadata(ItemSpecModifiers.Identity).ShouldBe("Monkey.txt");
        }

        [MSBuildTestMethod]
        public void RequestTimeStamps()
        {
            TaskItem from = new TaskItem();
            from.ItemSpec = FileUtilities.GetTemporaryFile();

            from.GetMetadata(ItemSpecModifiers.ModifiedTime).Length.ShouldBeGreaterThan(0);

            from.GetMetadata(ItemSpecModifiers.CreatedTime).Length.ShouldBeGreaterThan(0);

            from.GetMetadata(ItemSpecModifiers.AccessedTime).Length.ShouldBeGreaterThan(0);

            File.Delete(from.ItemSpec);

            from.GetMetadata(ItemSpecModifiers.ModifiedTime).Length.ShouldBe(0);

            from.GetMetadata(ItemSpecModifiers.CreatedTime).Length.ShouldBe(0);

            from.GetMetadata(ItemSpecModifiers.AccessedTime).Length.ShouldBe(0);
        }

        /// <summary>
        /// Verify metadata cannot be created with null name
        /// </summary>
        [MSBuildTestMethod]
        public void CreateNullNamedMetadata()
        {
            Should.Throw<ArgumentNullException>(() =>
            {
                TaskItem item = new TaskItem("foo");
                item.SetMetadata(null, "x");
            });
        }
        /// <summary>
        /// Verify metadata cannot be created with empty name
        /// </summary>
        [MSBuildTestMethod]
        public void CreateEmptyNamedMetadata()
        {
            Should.Throw<ArgumentException>(() =>
            {
                TaskItem item = new TaskItem("foo");
                item.SetMetadata("", "x");
            });
        }
        /// <summary>
        /// Create a TaskItem with a null metadata value -- this is allowed, but
        /// internally converted to the empty string.
        /// </summary>
        [MSBuildTestMethod]
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
        [MSBuildTestMethod]
        public void SetNullMetadataValue()
        {
            TaskItem item = new TaskItem("bar");
            item.SetMetadata("m", null);
            item.GetMetadata("m").ShouldBe(string.Empty);
        }

        [MSBuildTestMethod]
        public void ImplementsIMetadataContainer()
        {
            Dictionary<string, string> metadata = new()
            {
                { "a", "a1" },
                { "b", "b1" },
            };

            TaskItem item = new TaskItem("foo");
            IMetadataContainer metadataContainer = (IMetadataContainer)item;

            metadataContainer.ImportMetadata(metadata);

            var actualMetadata = metadataContainer.EnumerateMetadata().OrderBy(metadata => metadata.Key).ToList();
            var expectedMetadata = metadata.OrderBy(metadata => metadata.Value).ToList();
            Assert.IsTrue(actualMetadata.SequenceEqual(expectedMetadata));
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Test that task items can be successfully constructed based on a task item from another appdomain.
        /// </summary>
        [MSBuildTestMethod]
        public void RemoteTaskItem()
        {
            AppDomain appDomain = null;
            try
            {
                appDomain = AppDomain.CreateDomain(
                                "generateResourceAppDomain",
                                null,
                                AppDomain.CurrentDomain.SetupInformation);

                object obj = appDomain.CreateInstanceFromAndUnwrap(
                       typeof(TaskItemCreator).Module.FullyQualifiedName,
                       typeof(TaskItemCreator).FullName);

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

                    Dictionary<string, string> creatorMetadata = new Dictionary<string, string>(creator.CreatedTaskItems[i].MetadataCount);
                    foreach (string metadatum in creator.CreatedTaskItems[i].MetadataNames)
                    {
                        creatorMetadata[metadatum] = creator.CreatedTaskItems[i].GetMetadata(metadatum);
                    }

                    Dictionary<string, string> metadataInThisAppDomain = new Dictionary<string, string>(itemsInThisAppDomain[i].MetadataCount);
                    foreach (string metadatum in itemsInThisAppDomain[i].MetadataNames)
                    {
                        if (!string.Equals("OriginalItemSpec", metadatum))
                        {
                            metadataInThisAppDomain[metadatum] = itemsInThisAppDomain[i].GetMetadata(metadatum);
                        }
                    }

                    metadataInThisAppDomain.ShouldBe(creatorMetadata, ignoreOrder: true);
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
                ArgumentNullException.ThrowIfNull(includes);

                CreatedTaskItems = new TaskItem[includes.Length];

                for (int i = 0; i < includes.Length; i++)
                {
                    CreatedTaskItems[i] = new TaskItem(includes[i], (IDictionary)metadataToAdd);
                }
            }
        }
#endif
    }

    /// <summary>
    /// Tests for the generic <see cref="TaskItem{T}"/> struct.
    /// </summary>
    [TestClass]
    public class TaskItemOfTTests
    {
        [MSBuildTestMethod]
        public void SetMetadata_DoesNotThrow()
        {
            var item = new TaskItem<int>(42);
            // Should not throw - backing is a mutable TaskItem, not TaskItemData
            item.SetMetadata("key", "value");
            item.GetMetadata("key").ShouldBe("value");
        }

        [MSBuildTestMethod]
        public void RemoveMetadata_DoesNotThrow()
        {
            var item = new TaskItem<int>(42);
            item.SetMetadata("key", "value");
            item.RemoveMetadata("key");
            item.GetMetadata("key").ShouldBeNullOrEmpty();
        }

        [MSBuildTestMethod]
        public void CopyMetadataTo_DoesNotThrow()
        {
            var source = new TaskItem<int>(42);
            source.SetMetadata("key", "value");
            var dest = new TaskItem("dest");
            source.CopyMetadataTo(dest);
            dest.GetMetadata("key").ShouldBe("value");
        }

        [MSBuildTestMethod]
        public void FromITaskItem_PathLikeType_UsesFullPathMetadata()
        {
            // FullPath is a reserved metadata computed by the MSBuild item system as the absolute path.
            // TaskItem<FileInfo> should use FullPath so relative ItemSpecs resolve to absolute paths.
            var backingItem = new TaskItem("relative\\path.txt");
            string expectedAbsolutePath = backingItem.GetMetadata("FullPath");
            expectedAbsolutePath.ShouldNotBeNullOrEmpty();

            var item = new TaskItem<System.IO.FileInfo>(backingItem);
            item.Value.FullName.ShouldBe(expectedAbsolutePath);
        }

        [MSBuildTestMethod]
        public void FromITaskItem_NonPathType_UsesItemSpec()
        {
            var backingItem = new TaskItem("42");
            var item = new TaskItem<int>(backingItem);
            item.Value.ShouldBe(42);
        }

        [MSBuildTestMethod]
        public void Equals_SameValue_ReturnsTrue()
        {
            var a = new TaskItem<int>(42);
            var b = new TaskItem<int>(42);
            a.Equals(b).ShouldBeTrue();
            (a == b).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void Equals_DifferentValue_ReturnsFalse()
        {
            var a = new TaskItem<int>(1);
            var b = new TaskItem<int>(2);
            a.Equals(b).ShouldBeFalse();
            (a != b).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void Equals_NullStringValue_HandledCorrectly()
        {
            // EqualityComparer<string>.Default handles null without boxing or NullReferenceException
            var a = new TaskItem<string>((string)null!);
            var b = new TaskItem<string>((string)null!);
            a.Equals(b).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void GetHashCode_NullValue_ReturnsZero()
        {
            var item = new TaskItem<string>((string)null!);
            item.GetHashCode().ShouldBe(0);
        }

        [MSBuildTestMethod]
        public void GetHashCode_ConsistentWithEquality()
        {
            var a = new TaskItem<int>(42);
            var b = new TaskItem<int>(42);
            a.GetHashCode().ShouldBe(b.GetHashCode());
        }
    }
}
