// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Threading;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Xunit;

#pragma warning disable 0219

#if FEATURE_FILE_TRACKER

namespace Microsoft.Build.UnitTests.TrackedDependencies
{
    public sealed class TrackedDependenciesTests
    {
        private readonly int _sleepTimeMilliseconds = NativeMethodsShared.IsWindows ? 100 : 1000;

        public TrackedDependenciesTests()
        {
            string tempPath = Path.GetTempPath();
            string tempTestFilesPath = Path.Combine(tempPath, "TestFiles");

            if (Directory.Exists("TestFiles"))
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Directory.Delete("TestFiles", true /* recursive */);
                        break;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000);
                        // Eat exceptions from the delete
                    }
                }
            }

            if (Directory.Exists(tempTestFilesPath))
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Directory.Delete(tempTestFilesPath, true /* recursive */);
                        break;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(1000);
                        // Eat exceptions from the delete
                    }
                }
            }

            Directory.CreateDirectory(tempTestFilesPath);
            Directory.CreateDirectory("TestFiles");

            // Sleep for a period before each test is run so that
            // there is enough time for files to have distinct
            // last modified times - this ensures that the tracking
            // dependency caching of tracking logs (which is based on
            // last write time) can be relied upon
            Thread.Sleep(_sleepTimeMilliseconds);
        }

        /// <summary>
        /// Tests DependencyTableCache.FormatNormalizedTlogRootingMarker, which should do effectively the same 
        /// thing as FileTracker.FormatRootingMarker, except with some extra initial normalization to get rid of
        /// pesky PIDs and TIDs in the tlog names. 
        /// </summary>
        [Fact]
        public void FormatNormalizedRootingMarkerTests()
        {
            var tests = new Dictionary<ITaskItem[], string>
            {
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.9999-cvtres.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID]-cvtres.write.[ID].tlog")
                        .ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.0000-cvtres.read.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID]-cvtres.read.[ID].tlog")
                        .ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.4567-cvtres.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID]-cvtres.write.[ID].tlog")
                        .ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.9999.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID].write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.0000.read.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID].read.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.4567.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID].write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link2345.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link2345.write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("link.4567.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "link.[ID].write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\a.1234.b\\link.4567.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\a.1234.b\\link.[ID].write.[ID].tlog")
                        .ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("link.write.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "link.write.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("link%20with%20spaces.write.3.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "link with spaces.write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[2] {new TaskItem("link.write.tlog"), new TaskItem("Debug\\link2345.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link2345.write.[ID].tlog")
                        .ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "link.write.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("link.write.tlog1234")},
                    Path.Combine(Directory.GetCurrentDirectory(), "link.write.tlog1234").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("1234link.write.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "1234link.write.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("link-1234.write.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "link-1234.write.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("C:\\Debug\\a.1234.b\\link.4567.write.1.tlog")},
                    "C:\\DEBUG\\A.1234.B\\LINK.[ID].WRITE.[ID].TLOG"
                },
                {
                    new ITaskItem[] {new TaskItem("a\\")},
                    Path.Combine(Directory.GetCurrentDirectory(), "a\\").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.45\\67.write.1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.45\\67.write.[ID].tlog")
                        .ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("Debug\\link.4567.write.1.tlog\\")},
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.4567.write.1.tlog\\").ToUpperInvariant()
                },
                {new ITaskItem[0] { }, ""},
                {
                    new ITaskItem[3]
                    {
                        new TaskItem("Debug\\link.write.1.tlog"),
                        new TaskItem("Debug\\link.2345.write.1.tlog"),
                        new TaskItem("Debug\\link.2345-cvtres.6789-mspdbsrv.1111.write.4.tlog")
                    },
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.write.[ID].tlog").ToUpperInvariant() +
                    "|" +
                    Path.Combine(Directory.GetCurrentDirectory(),
                        "Debug\\link.[ID]-cvtres.[ID]-mspdbsrv.[ID].write.[ID].tlog").ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "Debug\\link.[ID].write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[3]
                    {
                        new TaskItem("link.1234-write.1.tlog"), new TaskItem("link.1234-write.3.tlog"),
                        new TaskItem("cl.write.2.tlog")
                    },
                    Path.Combine(Directory.GetCurrentDirectory(), "cl.write.[ID].tlog").ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "link.[ID]-write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[3]
                    {
                        new TaskItem("lINk.1234-write.1.tlog"), new TaskItem("link.1234-WRitE.3.tlog"),
                        new TaskItem("cl.write.2.tlog")
                    },
                    Path.Combine(Directory.GetCurrentDirectory(), "cl.write.[ID].tlog").ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "link.[ID]-write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[3]
                    {
                        new TaskItem("a\\link.1234-write.1.tlog"), new TaskItem("b\\link.1234-write.3.tlog"),
                        new TaskItem("cl.write.2.tlog")
                    },
                    Path.Combine(Directory.GetCurrentDirectory(), "a\\link.[ID]-write.[ID].tlog")
                        .ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "b\\link.[ID]-write.[ID].tlog")
                        .ToUpperInvariant() + "|" +
                    Path.Combine(Directory.GetCurrentDirectory(), "cl.write.[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("foo\\.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "foo\\.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("foo\\1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), "foo\\1.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("\\1.tlog")},
                    Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), "1.tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem(".1.tlog")},
                    Path.Combine(Directory.GetCurrentDirectory(), ".[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("-2")},
                    Path.Combine(Directory.GetCurrentDirectory(), "-2").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem(".2")},
                    Path.Combine(Directory.GetCurrentDirectory(), ".2").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("2-")},
                    Path.Combine(Directory.GetCurrentDirectory(), "2-").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("2.")},
                    Path.Combine(Directory.GetCurrentDirectory(), "2").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("\\.1.tlog")},
                    Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()), ".[ID].tlog").ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("\\")},
                    Path.GetPathRoot(Directory.GetCurrentDirectory()).ToUpperInvariant()
                },
                {
                    new ITaskItem[] {new TaskItem("\\\\share\\foo.read.8.tlog")},
                    "\\\\share\\foo.read.[ID].tlog".ToUpperInvariant()
                }
            };
            foreach (KeyValuePair<ITaskItem[], string> test in tests)
            {
                Assert.Equal(test.Value, DependencyTableCache.FormatNormalizedTlogRootingMarker(test.Key)); // "Incorrectly formatted rooting marker"
            }

            bool exceptionCaught = false;
            try
            {
                DependencyTableCache.FormatNormalizedTlogRootingMarker(new ITaskItem[] { new TaskItem("\\\\") });
            }
            catch (ArgumentException)
            {
                exceptionCaught = true;
            }

            Assert.True(exceptionCaught); // "Should have failed to format a rooting marker from a malformed UNC path"
        }

        [Fact]
        public void CreateTrackedDependencies()
        {
            Console.WriteLine("Test: CreateTrackedDependencies");
            ITaskItem[] sources = null;
            ITaskItem[] outputs = null;
            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    null,
                    sources,
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );
            Assert.NotNull(d);
        }

        [Fact]
        public void SingleCanonicalCL()
        {
            Console.WriteLine("Test: SingleCanonicalCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Single(outofdate);
            Assert.Equal("TestFiles\\one.cpp", outofdate[0].ItemSpec);
        }

        [Fact]
        public void NonExistentTlog()
        {
            Console.WriteLine("Test: NonExistentTlog");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            // Just to be sure, delete the test tlog.
            File.Delete(Path.Combine("TestFiles", "one.tlog"));

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Single(outofdate);
            Assert.Equal(outofdate[0].ItemSpec, Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void EmptyTLog()
        {
            Console.WriteLine("Test: EmptyTLog");

            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tlog"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Single(outofdate);
            Assert.Equal(outofdate[0].ItemSpec, Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void InvalidReadTLogName()
        {
            Console.WriteLine("Test: InvalidReadTLogName");

            // Prepare files
            DependencyTestHelper.WriteAll("TestFiles\\one.h", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.cpp", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.obj", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.tlog", "");

            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\|one|.tlog")),
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\one.cpp")),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\one.obj")),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            d.ComputeSourcesNeedingCompilation();

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have an error."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void ReadTLogWithInitialEmptyLine()
        {
            Console.WriteLine("Test: ReadTLogWithInitialEmptyLine");

            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "", "^FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void ReadTLogWithEmptyLineImmediatelyAfterRoot()
        {
            Console.WriteLine("Test: ReadTLogWithEmptyLineImmediatelyAfterRoot");

            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^FOO", "", "FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void ReadTLogWithEmptyLineBetweenRoots()
        {
            Console.WriteLine("Test: ReadTLogWithEmptyLineImmediatelyAfterRoot");

            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^FOO", "FOO", "", "^BAR", "BAR" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void ReadTLogWithEmptyRoot()
        {
            Console.WriteLine("Test: ReadTLogWithEmptyRoot");

            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^", "FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void ReadTLogWithDuplicateInRoot()
        {
            Console.WriteLine("Test: ReadTLogWithDuplicateInRoot");

            //Prepare files
            DependencyTestHelper.WriteAll("TestFiles\\one.h", "");
            DependencyTestHelper.WriteAll("TestFiles\\foo.cpp", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.obj", "");

            ITaskItem[] sources = { new TaskItem("TestFiles\\foo.cpp"), new TaskItem("TestFiles\\foo.cpp") };

            File.WriteAllLines("TestFiles\\one.tlog", new[] { "^TestFiles\\foo.cpp|TestFiles\\foo.cpp", "TestFiles\\bar.cpp", "TestFiles\\foo.cpp" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\one.tlog")),
                    sources,
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\one.obj")),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.NotEmpty(d.DependencyTable); // "Dependency Table should not be empty."
        }

        [Fact]
        public void InvalidWriteTLogName()
        {
            Console.WriteLine("Test: InvalidWriteTLogName");

            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\|one|.write.tlog"))
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have an error."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void WriteTLogWithInitialEmptyLine()
        {
            Console.WriteLine("Test: WriteTLogWithInitialEmptyLine");

            // Prepare files
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] { "", "^FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog")))
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void WriteTLogWithEmptyLineImmediatelyAfterRoot()
        {
            Console.WriteLine("Test: ReadTLogWithEmptyLineImmediatelyAfterRoot");

            // Prepare files
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] { "^FOO", "", "FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog")))
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void WriteTLogWithEmptyLineBetweenRoots()
        {
            Console.WriteLine("Test: WriteTLogWithEmptyLineImmediatelyAfterRoot");

            // Prepare files
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] { "^FOO", "FOO", "", "^BAR", "BAR" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog")))
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void WriteTLogWithEmptyRoot()
        {
            Console.WriteLine("Test: WriteTLogWithEmptyRoot");

            // Prepare files
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] { "^", "FOO" });
            MockTask task = DependencyTestHelper.MockTask;

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog")))
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(d.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void PrimarySourceNotInTlog()
        {
            Console.WriteLine("Test: PrimarySourceNotInTlog");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            // Primary Source; not appearing in this Tlog..
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "foo.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "foo.h")),
            });

            // Touch the obj - normally this would mean uptodate, but since there
            // is no tlog entry for the primary source, we want a rebuild of it.
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Single(outofdate);
            Assert.Equal(outofdate[0].ItemSpec, Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleCanonicalCL()
        {
            Console.WriteLine("Test: MultipleCanonicalCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Single(outofdate);
            Assert.Equal(outofdate[0].ItemSpec, Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleCanonicalCLCompactMissingOnSuccess()
        {
            Console.WriteLine("Test: MultipleCanonicalCLCompactMissingOnSuccess");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile.obj"))
            });

            CanonicalTrackedOutputFiles compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            compactOutputs.RemoveDependenciesFromEntryIfMissing(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
            compactOutputs.SaveTlog();

            // Compact the read tlog
            CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    compactOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            compactInputs.RemoveDependenciesFromEntryIfMissing(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
            compactInputs.SaveTlog();

            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Empty(outofdate);
        }

        [Fact]
        public void MultipleCanonicalCLCompactMissingOnSuccessMultiEntry()
        {
            Console.WriteLine("Test: MultipleCanonicalCLCompactMissingOnSuccessMultiEntry");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile2.obj"))
            });

            CanonicalTrackedOutputFiles compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            compactOutputs.RemoveDependenciesFromEntryIfMissing(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
            compactOutputs.SaveTlog();
            // Compact the read tlog
            CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    compactOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            compactInputs.RemoveDependenciesFromEntryIfMissing(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
            compactInputs.SaveTlog();

            CanonicalTrackedOutputFiles writtenOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            CanonicalTrackedInputFiles writtenInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    writtenOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.Single(writtenOutputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))]);
            Assert.Equal(4, writtenInputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))].Count);
            // Everything to do with two.cpp should be left intact
            Assert.Equal(2, writtenOutputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))].Count);
            Assert.Equal(3, writtenInputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))].Count);
        }

        [Fact]
        public void RemoveDependencyFromEntry()
        {
            Console.WriteLine("Test: RemoveDependencyFromEntry");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tlh"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tli"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
            });

            CanonicalTrackedOutputFiles compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            compactOutputs.RemoveDependencyFromEntry(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
            compactOutputs.SaveTlog();

            CanonicalTrackedOutputFiles writtenOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            Assert.False(writtenOutputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))].ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));

            CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    compactOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            compactInputs.RemoveDependencyFromEntry(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
            compactInputs.SaveTlog();

            CanonicalTrackedInputFiles writtenInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    writtenOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            Assert.False(writtenInputs.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))].ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
        }

        [Fact]
        public void RemoveDependencyFromEntries()
        {
            Console.WriteLine("Test: RemoveDependencyFromEntry");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tlh"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tli"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register

            string rootingMarker = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"));

            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + rootingMarker,
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + rootingMarker,
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
            });

            CanonicalTrackedOutputFiles compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            compactOutputs.RemoveDependencyFromEntry(new[] { new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"))) }, new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
            compactOutputs.SaveTlog();

            CanonicalTrackedOutputFiles writtenOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            Assert.False(writtenOutputs.DependencyTable[rootingMarker].ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));

            CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    new[] { new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"))) },
                    null,
                    compactOutputs,
                    false, /* no minimal rebuild optimization */
                    true /* shred composite rooting markers */
                );

            compactInputs.RemoveDependencyFromEntry(new[] { new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"))) }, new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
            compactInputs.SaveTlog();

            CanonicalTrackedInputFiles writtenInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    new[] { new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))), new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"))) },
                    null,
                    writtenOutputs,
                    false, /* no minimal rebuild optimization */
                    true /* shred composite rooting markers */
                );

            Assert.False(writtenInputs.DependencyTable[rootingMarker].ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one3.obj"))));
        }

        [Fact]
        public void RemoveRootsWithSharedOutputs()
        {
            Console.WriteLine("Test: RemoveRootsWithSharedOutputs");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tlh"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tli"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register

            string rootingMarker1 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"));
            string rootingMarker2 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"));
            string rootingMarker3 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"));

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + rootingMarker1.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + rootingMarker2.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
                "^" + rootingMarker3.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.obj")),
            });

            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker1));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker2));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker3));

            outputs.RemoveRootsWithSharedOutputs(new ITaskItem[] { new TaskItem(Path.Combine("TestFiles", "one.cpp")), new TaskItem(Path.Combine("TestFiles", "three.cpp")), new TaskItem(Path.Combine("TestFiles", "two.cpp")) });

            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker1));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker2));
            Assert.False(outputs.DependencyTable.ContainsKey(rootingMarker3));
        }

        [Fact]
        public void RemoveRootsWithSharedOutputs_CurrentRootNotInTable()
        {
            Console.WriteLine("Test: RemoveRootsWithSharedOutputs");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tlh"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.tli"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register

            string rootingMarker1 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"));
            string rootingMarker2 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"));
            string rootingMarker3 = Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"));

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + rootingMarker1.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + rootingMarker2.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tlh")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.tli")),
                "^" + rootingMarker3.ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.obj")),
            });

            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker1));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker2));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker3));

            outputs.RemoveRootsWithSharedOutputs(new ITaskItem[] { new TaskItem(Path.Combine("TestFiles", "four.cpp")), new TaskItem(Path.Combine("TestFiles", "one.cpp")), new TaskItem(Path.Combine("TestFiles", "three.cpp")), new TaskItem(Path.Combine("TestFiles", "two.cpp")) });

            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker1));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker2));
            Assert.True(outputs.DependencyTable.ContainsKey(rootingMarker3));
        }

        [Fact]
        public void MultipleCanonicalCLMissingDependency()
        {
            Console.WriteLine("Test: MultipleCanonicalCLMissingDependency");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Delete one of our dependencies
            string missing = Path.GetFullPath(Path.Combine("TestFiles", "one2.h"));
            File.Delete(missing);

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // We're out of date, since a missing dependency indicates out-of-dateness
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);

            // The dependency has been recorded and retrieved correctly
            Assert.True(d.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))].ContainsKey(missing));

            // Save out the compacted read log - our missing dependency will be compacted away
            // The tlog will have to entries compacted, since we're not up to date
            d.RemoveEntriesForSource(d.SourcesNeedingCompilation);
            d.SaveTlog();

            // read the tlog back in again
            d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // We're out of date, since a missing dependency indicates out-of-dateness
            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);

            // We have a source outstanding for recompilation, it will not appear in
            // the tracking information as it will be written again
            Assert.False(d.DependencyTable.ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
        }

        [Fact]
        public void MultipleCanonicalCLMissingOutputDependencyRemoved()
        {
            Console.WriteLine("Test: MultipleCanonicalCLMissingOutputDependencyRemoved");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile2.obj"))
            });

            string missing = Path.GetFullPath(Path.Combine("TestFiles", "sometempfile2.obj"));

            CanonicalTrackedOutputFiles compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            // Save out the compacted read log - our missing dependency will be compacted away
            // Use an anonymous method to encapsulate the contains check for the tlogs
            compactOutputs.SaveTlog(delegate (string fullTrackedPath)
            {
                // We need to answer the question "should fullTrackedPath be included in the TLog?"
                return !string.Equals(fullTrackedPath, missing, StringComparison.OrdinalIgnoreCase);
            });

            // Read the Tlogs back in..
            compactOutputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));
            // Compact the read tlog
            CanonicalTrackedInputFiles compactInputs = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    new[] { new TaskItem(Path.Combine("TestFiles", "one.cpp")), new TaskItem(Path.Combine("TestFiles", "two.cpp")) },
                    null,
                    compactOutputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            compactInputs.SaveTlog();

            ITaskItem[] outofDate = compactInputs.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofDate);
        }

        [Fact]
        public void MultipleCanonicalCLMissingInputDependencyRemoved()
        {
            Console.WriteLine("Test: MultipleCanonicalCLMissingInputDependencyRemoved");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Delete one of our dependencies
            string missing = Path.GetFullPath(Path.Combine("TestFiles", "one2.h"));
            File.Delete(missing);

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // We're out of date, since a missing dependency indicates out-of-dateness
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);

            // The dependency has been recorded and retrieved correctly
            Assert.True(d.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))].ContainsKey(missing));

            // Save out the compacted read log - our missing dependency will be compacted away
            // Use an anonymous method to encapsulate the contains check for the tlogs
            d.SaveTlog(delegate (string fullTrackedPath)
            {
                // We need to answer the question "should fullTrackedPath be included in the TLog?"
                return !string.Equals(fullTrackedPath, missing, StringComparison.OrdinalIgnoreCase);
            });

            // read the tlog back in again
            d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // We're not out of date, since the missing dependency has been removed
            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofdate);
        }

        [Fact]
        public void MultiplePrimaryCanonicalCL()
        {
            Console.WriteLine("Test: MultiplePrimaryCanonicalCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    new ITaskItem[] {
                        new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                        new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                        },
                    null,
                    new ITaskItem[] {
                        new TaskItem(Path.Combine("TestFiles", "one.obj")),
                        new TaskItem(Path.Combine("TestFiles", "two.obj")),
                        },
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 2);
            Assert.True((outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[1].ItemSpec == Path.Combine("TestFiles", "two.cpp")) ||
                             (outofdate[1].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp")));
        }

        [Fact]
        public void MultiplePrimaryCanonicalCLUnderTemp()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string tempPath = Path.GetTempPath();

            try
            {
                Directory.SetCurrentDirectory(tempPath);

                Console.WriteLine("Test: MultiplePrimaryCanonicalCL");
                // Prepare files
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");

                Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
                File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                    "#Command some-command",
                    "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                    Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                    Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                    Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                    "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                    Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                    Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                    Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
                });

                // Touch one
                Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
                Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
                DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");

                CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                    (
                        DependencyTestHelper.MockTask,
                        DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                        new ITaskItem[] {
                            new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                            new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                            },
                        null,
                        new ITaskItem[] {
                            new TaskItem(Path.Combine("TestFiles", "one.obj")),
                            new TaskItem(Path.Combine("TestFiles", "two.obj")),
                            },
                        false, /* no minimal rebuild optimization */
                        false /* shred composite rooting markers */
                    );

                ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

                Assert.True(outofdate.Length == 2);
                Assert.True((outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[1].ItemSpec == Path.Combine("TestFiles", "two.cpp")) ||
                                 (outofdate[1].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp")));
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDirectory);
            }
        }

        [Fact]
        public void MultiplePrimaryCanonicalCLSharedDependency()
        {
            Console.WriteLine("Test: MultiplePrimaryCanonicalCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")), // the shared dependency
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")), // the shared dependency
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    new ITaskItem[] {
                        new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                        new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                        },
                    null,
                    new ITaskItem[] {
                        new TaskItem(Path.Combine("TestFiles", "one.obj")),
                        new TaskItem(Path.Combine("TestFiles", "two.obj")),
                        },
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 2);
            Assert.True((outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[1].ItemSpec == Path.Combine("TestFiles", "two.cpp")) ||
                             (outofdate[1].ItemSpec == Path.Combine("TestFiles", "one.cpp") && outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp")));
        }

        [Fact]
        public void MultipleCanonicalCLAcrossCommand1()
        {
            Console.WriteLine("Test: MultipleCanonicalCLAcrossCommand1");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                "#Command some-command1",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleCanonicalCLAcrossCommand2()
        {
            Console.WriteLine("Test: MultipleCanonicalCLAcrossCommand2");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                "#Command some-command1",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleCanonicalCLAcrossCommandNonDependency()
        {
            Console.WriteLine("Test: MultipleCanonicalCLAcrossCommandNonDependency");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")), // this root marker represents the end of the dependencies for one.cpp
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Empty(outofdate);
        }

        [Fact]
        public void MultipleCanonicalCLAcrossTlogs1()
        {
            Console.WriteLine("Test: MultipleCanonicalCLAcrossTlogs1");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.tlog"), new[] {
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleCanonicalCLAcrossTlogs2()
        {
            Console.WriteLine("Test: MultipleCanonicalCLAcrossTlogs2");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.tlog"), new[] {
                "#Command some-command1",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void SingleRootedCL()
        {
            Console.WriteLine("Test: SingleRootedCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleRootedCLAcrossTlogs1()
        {
            Console.WriteLine("Test: MultipleRootedCLAcrossTlogs1");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.tlog"), new[] {
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void MultipleRootedCL()
        {
            Console.WriteLine("Test: MultipleRootedCL");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "two.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp"));
        }

        [Fact]
        public void MultipleRootedCLNonDependency()
        {
            Console.WriteLine("Test: MultipleRootedCLNonDependency");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")), // this root marker represents the end of the dependencies for one.cpp
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.Empty(outofdate);
        }

        [Fact]
        public void MultipleRootedCLAcrossTlogs2()
        {
            Console.WriteLine("Test: MultipleRootedCLAcrossTlogs2");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.tlog"), new[] {
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));
        }

        [Fact]
        public void OutputSingleCanonicalCL()
        {
            Console.WriteLine("Test: OutputSingleCanonicalCL");
            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));

            Assert.True(outputs.Length == 1);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
        }

        [Fact]
        public void OutputSingleCanonicalCLAcrossTlogs()
        {
            Console.WriteLine("Test: OutputSingleCanonicalCLAcrossTlogs");
            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "two.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.pch")),
            });

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "two.tlog"))
                                };

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    tlogs);

            ITaskItem[] outputs = d.OutputsForSource(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));

            Assert.True(outputs.Length == 2);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "one.pch")));
        }

        [Fact]
        public void OutputNonExistentTlog()
        {
            Console.WriteLine("Test: NonExistentTlog");

            // Just to be sure, delete the test tlog.
            File.Delete(Path.Combine("TestFiles", "one.tlog"));

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));

            Assert.Null(outputs);
        }

        [Fact]
        public void OutputMultipleCanonicalCL()
        {
            Console.WriteLine("Test: OutputMultipleCanonicalCL");

            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(sources);

            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        [Fact]
        public void OutputMultipleCanonicalCLSubrootMatch()
        {
            Console.WriteLine("Test: OutputMultipleCanonicalCLSubrootMatch");

            // sources is a subset of source2
            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};
            ITaskItem[] sources2 = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "four.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "five.cpp")))};

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
                "^" + FileTracker.FormatRootingMarker(sources2),
                Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(sources2, /*searchForSubRootsInCompositeRootingMarkers*/ false);

            Assert.True(outputs.Length == 5);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")));
            Assert.True(outputs[3].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")));
            Assert.True(outputs[4].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")));

            ITaskItem[] outputs2 = d.OutputsForSource(sources2, /*searchForSubRootsInCompositeRootingMarkers*/ true);

            Assert.True(outputs2.Length == 8);
            Assert.True(outputs2[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs2[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs2[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
            Assert.True(outputs2[3].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")));
            Assert.True(outputs2[4].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")));
            Assert.True(outputs2[5].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")));
            Assert.True(outputs2[6].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")));
            Assert.True(outputs2[7].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")));

            // Test if sources can find the superset.
            ITaskItem[] outputs3 = d.OutputsForSource(sources, /*searchForSubRootsInCompositeRootingMarkers*/ true);

            Assert.True(outputs3.Length == 8);
            Assert.True(outputs3[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs3[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs3[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
            Assert.True(outputs3[3].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")));
            Assert.True(outputs3[4].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")));
            Assert.True(outputs3[5].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")));
            Assert.True(outputs3[6].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")));
            Assert.True(outputs3[7].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")));

            ITaskItem[] outputs4 = d.OutputsForSource(sources, /*searchForSubRootsInCompositeRootingMarkers*/ false);

            Assert.True(outputs4.Length == 3);
            Assert.True(outputs4[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs4[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs4[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        [Fact]
        public void OutputMultipleCanonicalCLSubrootMisMatch()
        {
            Console.WriteLine("Test: OutputMultipleCanonicalCLSubrootMisMatch");

            // sources is NOT a subset of source
            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};
            ITaskItem[] sources2 = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "four.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "five.cpp")))};
            ITaskItem[] sources2Match = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "four.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "five.cpp")))};
            ITaskItem[] sourcesPlusOne = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "eight.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            // Do note sources2Match and source2 is missing three.cpp.  It is to test if the RootContainsAllSubRootComponents can handle the case. 

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
                "^" + FileTracker.FormatRootingMarker(sources2),
                Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(sources2Match, /*searchForSubRootsInCompositeRootingMarkers*/ false);

            Assert.True(outputs.Length == 5);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")));
            Assert.True(outputs[3].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")));
            Assert.True(outputs[4].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")));

            ITaskItem[] outputs2 = d.OutputsForSource(sources2Match, /*searchForSubRootsInCompositeRootingMarkers*/ true);

            Assert.True(outputs2.Length == 5);
            Assert.True(outputs2[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fOUr.obj")));
            Assert.True(outputs2[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "fIve.obj")));
            Assert.True(outputs2[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sIx.obj")));
            Assert.True(outputs2[3].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "sEvEn.obj")));
            Assert.True(outputs2[4].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "EIght.obj")));

            ITaskItem[] outputs3 = d.OutputsForSource(sourcesPlusOne, /*searchForSubRootsInCompositeRootingMarkers*/ true);

            Assert.True(outputs3.Length == 3);
            Assert.True(outputs3[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs3[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs3[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));

            ITaskItem[] outputs4 = d.OutputsForSource(sourcesPlusOne, /*searchForSubRootsInCompositeRootingMarkers*/ false);

            Assert.Empty(outputs4);
        }

        [Fact]
        public void OutputMultipleCanonicalCLLongTempPath()
        {
            Console.WriteLine("Test: OutputMultipleCanonicalCLLongTempPath");

            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            string oldTempPath = Environment.GetEnvironmentVariable("TEMP");
            string oldTmpPath = Environment.GetEnvironmentVariable("TMP");
            string newTempPath = Path.GetFullPath(Path.Combine("TestFiles", "ThisIsAReallyVeryLongTemporaryPlace", "ThatIsLongerThanTheSourcePaths"));

            Directory.CreateDirectory(newTempPath);
            Environment.SetEnvironmentVariable("TEMP", newTempPath);
            Environment.SetEnvironmentVariable("TMP", newTempPath);

            Console.WriteLine("Test: OutputMultipleCanonicalCL");

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(sources);

            Environment.SetEnvironmentVariable("TEMP", oldTempPath);
            Environment.SetEnvironmentVariable("TMP", oldTmpPath);

            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        [Fact]
        public void OutputMultipleCanonicalCLAcrossTLogs()
        {
            Console.WriteLine("Test: OutputMultipleCanonicalCLAcrossTLogs");

            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "two.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "two.tlog"))
                                };

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    tlogs);

            ITaskItem[] outputs = d.OutputsForSource(sources);

            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        [Fact]
        public void OutputMultipleSingleSubRootCanonicalCL()
        {
            Console.WriteLine("Test: OutputMultipleSingleSubRootCanonicalCL");

            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))));

            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        [Fact]
        public void OutputMultipleUnrecognisedRootCanonicalCL()
        {
            Console.WriteLine("Test: OutputMultipleUnrecognisedRootCanonicalCL");

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))));

            ITaskItem[] outputs = d.OutputsForSource(new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "four.cpp"))));

            Assert.Empty(outputs);
        }

        [Fact]
        public void OutputCLMinimalRebuildOptimization()
        {
            Console.WriteLine("Test: OutputCLMinimalRebuildOptimization");

            // Prepare read tlog
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Our source files
            ITaskItem[] sources = {
                                    new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "three.cpp")),
                                };

            // Prepare write tlog
            // This includes individual output information for each root
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            // Represent our tracked and computed outputs
            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            // Represent our tracked and provided inputs
            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    sources,
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // First of all, all things should be up to date
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofdate);

            // Delete one of the outputs in the group
            File.Delete(Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));

            // With optimization off, all sources in the group will need compilation
            d.SourcesNeedingCompilation = null;
            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Equal(3, outofdate.Length);

            // With optimization on, only the source that matches the output will need compilation
            d = new CanonicalTrackedInputFiles
                    (
                        DependencyTestHelper.MockTask,
                        DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                        sources,
                        null,
                        outputs,
                        true, /* enable minimal rebuild optimization */
                        false /* shred composite rooting markers */
                    );

            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);
            // And the source is.. two.cpp!
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp"));
        }

        [Fact]
        public void OutputCLMinimalRebuildOptimizationComputed()
        {
            Console.WriteLine("Test: OutputCLMinimalRebuildOptimizationComputed");

            // Prepare read tlog
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Our source files
            ITaskItem[] sources = {
                                    new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "three.cpp")),
                                };

            // Prepare write tlog
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + FileTracker.FormatRootingMarker(sources),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            // Represent our tracked and computed outputs
            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            // "Compute" the additional output information for this compilation, rather than them being tracked
            outputs.AddComputedOutputForSourceRoot(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")), Path.GetFullPath(Path.Combine("TestFiles", "one.obj")));
            outputs.AddComputedOutputForSourceRoot(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")), Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            outputs.AddComputedOutputForSourceRoot(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")), Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));

            // Represent our tracked and provided inputs
            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    sources,
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // First of all, all things should be up to date
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofdate);

            // Delete one of the outputs in the group
            File.Delete(Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));

            // With optimization off, all sources in the group will need compilation
            d.SourcesNeedingCompilation = null;
            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Equal(3, outofdate.Length);

            // With optimization on, only the source that matches the output will need compilation
            d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    sources,
                    null,
                    outputs,
                    true, /* enable minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);
            // And the source is.. two.cpp!
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "two.cpp"));
        }

        [Fact]
        public void ReplaceOutputForSource()
        {
            Console.WriteLine("Test: ReplaceOutputForSource");

            if (File.Exists(Path.GetFullPath(Path.Combine("TestFiles", "three.i"))))
            {
                File.Delete(Path.GetFullPath(Path.Combine("TestFiles", "three.i")));
            }

            // Prepare read tlog
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            // Our source files
            ITaskItem[] sources = {
                                    new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "three.cpp")),
                                };

            // Prepare write tlog
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
            });

            // Represent our tracked and computed outputs
            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            // Change the output (note that this doesn't affect the timestamp)
            File.Move(Path.GetFullPath(Path.Combine("TestFiles", "three.obj")), Path.GetFullPath(Path.Combine("TestFiles", "three.i")));

            string threeRootingMarker = FileTracker.FormatRootingMarker(new TaskItem(Path.Combine("TestFiles", "three.cpp")));
            // Remove the fact that three.obj was the tracked output
            bool removed = outputs.RemoveOutputForSourceRoot(threeRootingMarker, Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
            Assert.True(removed);
            // "Compute" the replacement output information for this compilation, rather than the one originally tracked
            outputs.AddComputedOutputForSourceRoot(threeRootingMarker, Path.GetFullPath(Path.Combine("TestFiles", "three.i")));

            // Represent our tracked and provided inputs
            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    sources,
                    null,
                    outputs,
                    true, /* minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // We should have one output for three.cpp
            Assert.Single(outputs.DependencyTable[threeRootingMarker]);
            Assert.False(outputs.DependencyTable[threeRootingMarker].ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "three.obj"))));

            // All things should be up to date
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofdate);

            // Delete the new output
            File.Delete(Path.GetFullPath(Path.Combine("TestFiles", "three.i")));

            // This means a recompile would be required for the roots
            d.SourcesNeedingCompilation = null;
            outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Single(outofdate);
        }

        [Fact]
        public void ExcludeSpecificDirectory()
        {
            Console.WriteLine("Test: ExcludeSpecificDirectory");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds);

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds);

            Directory.CreateDirectory(Path.Combine("TestFiles", "Foo"));
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "Foo", "one2.h"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register

            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "Foo", "one2.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")).ToUpperInvariant(),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "Foo", "one2.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")).ToUpperInvariant(),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "Foo", "one2.h")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")).ToUpperInvariant(),
            });

            // Our source files
            ITaskItem[] sources = {
                                    new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                                    new TaskItem(Path.Combine("TestFiles", "three.cpp")),
                                };

            // Prepare write tlog
            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")).ToUpperInvariant(),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")).ToUpperInvariant(),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")).ToUpperInvariant(),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")).ToUpperInvariant(),
            });

            // Represent our tracked and computed outputs
            CanonicalTrackedOutputFiles outputs = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))));

            // Represent our tracked and provided inputs
            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))),
                    sources,
                    new[] { new TaskItem(Path.GetFullPath(Path.Combine("TeSTfiles", "Foo"))) },
                    outputs,
                    true, /* minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            // All things should be up to date
            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();
            Assert.Empty(outofdate);
        }

        [Fact]
        public void SaveCompactedReadTlog()
        {
            Console.WriteLine("Test: SaveCompactedReadTlog");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.tlog"), new[] {
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "two1.tlog"), new[] {
                "#Command some-command2",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            // Touch one
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "two1.tlog"))
                                };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));

            d.RemoveEntriesForSource(d.SourcesNeedingCompilation);
            d.SaveTlog();

            // All the tlogs need to still be there even after compaction
            // It's OK for them to be empty, but their absence might mean a partial clean
            // A missing tlog would mean a clean build
            Assert.True(Microsoft.Build.Utilities.TrackedDependencies.ItemsExist(tlogs));

            // There should be no difference in the out of date files after compaction
            CanonicalTrackedInputFiles d1 = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            outofdate = d1.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 1);
            Assert.True(outofdate[0].ItemSpec == Path.Combine("TestFiles", "one.cpp"));

            ITaskItem[] tlogs2 = {
                                    tlogs[0]
                                 };

            // All log information should now be in the tlog[0]
            CanonicalTrackedInputFiles d2 = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs2,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "two.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "two.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            outofdate = d2.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 0);
            Assert.True(d2.DependencyTable.Count == 1);
            Assert.False(d2.DependencyTable.ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));

            // There should be no difference even if we send in all the original tlogs
            CanonicalTrackedInputFiles d3 = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "two.cpp"))),
                    null,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "two.obj"))),
                    false, /* no minimal rebuild optimization */
                    false /* shred composite rooting markers */
                );

            outofdate = d3.ComputeSourcesNeedingCompilation();

            Assert.True(outofdate.Length == 0);
            Assert.True(d3.DependencyTable.Count == 1);
            Assert.False(d3.DependencyTable.ContainsKey(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))));
        }

        [Fact]
        public void SaveCompactedWriteTlog()
        {
            Console.WriteLine("Test: SaveCompactedWriteTlog");
            TaskItem fooItem = new TaskItem("foo");

            ITaskItem[] sources = {
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))),
                                    new TaskItem(Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")))};

            string rootMarker = FileTracker.FormatRootingMarker(sources);

            // Prepare files
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                "^" + rootMarker,
                Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")),
                "^" + fooItem.GetMetadata("Fullpath"),
                Path.GetFullPath(Path.Combine("TestFiles", "foo1.bar")),
                Path.GetFullPath(Path.Combine("TestFiles", "bar1.baz")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "two.tlog"), new[] {
                "#Command some-command",
                "^" + rootMarker,
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "three.obj")),
                "^" + fooItem.GetMetadata("Fullpath"),
                Path.GetFullPath(Path.Combine("TestFiles", "foo2.bar")),
                Path.GetFullPath(Path.Combine("TestFiles", "bar2.baz")),
            });

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "two.tlog"))
                                };

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            CanonicalTrackedOutputFiles d = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    tlogs);

            ITaskItem[] outputs = d.OutputsForSource(sources);

            Assert.Equal(3, outputs.Length);
            Assert.Equal(outputs[0].ItemSpec, Path.GetFullPath("TestFiles\\oNe.obj"));
            Assert.Equal(outputs[1].ItemSpec, Path.GetFullPath("TestFiles\\two.obj"));
            Assert.Equal(outputs[2].ItemSpec, Path.GetFullPath("TestFiles\\three.obj"));

            outputs = d.OutputsForSource(fooItem);
            Assert.Equal(4, outputs.Length);
            Assert.Equal(outputs[0].ItemSpec, Path.GetFullPath("TestFiles\\foo1.bar"));
            Assert.Equal(outputs[1].ItemSpec, Path.GetFullPath("TestFiles\\bar1.baz"));
            Assert.Equal(outputs[2].ItemSpec, Path.GetFullPath("TestFiles\\foo2.bar"));
            Assert.Equal(outputs[3].ItemSpec, Path.GetFullPath("TestFiles\\bar2.baz"));

            // Compact the tlog removing all entries for "foo" leaving the other entries intact
            d.RemoveEntriesForSource(fooItem);
            d.SaveTlog();

            // All the tlogs need to still be there even after compaction
            // It's OK for them to be empty, but their absence might mean a partial clean
            // A missing tlog would mean a clean build
            Assert.True(Microsoft.Build.Utilities.TrackedDependencies.ItemsExist(tlogs));

            // All log information should now be in the tlog[0]
            ITaskItem[] tlogs2 = {
                                    tlogs[0]
                                 };

            CanonicalTrackedOutputFiles d2 = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    tlogs2);

            outputs = d2.OutputsForSource(fooItem);
            Assert.Empty(outputs);

            outputs = d2.OutputsForSource(sources);
            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));

            // There should be no difference even if we send in all the original tlogs
            CanonicalTrackedOutputFiles d3 = new CanonicalTrackedOutputFiles(DependencyTestHelper.MockTask,
                    tlogs);

            outputs = d3.OutputsForSource(fooItem);
            Assert.Empty(outputs);

            outputs = d3.OutputsForSource(sources);
            Assert.True(outputs.Length == 3);
            Assert.True(outputs[0].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "oNe.obj")));
            Assert.True(outputs[1].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "two.obj")));
            Assert.True(outputs[2].ItemSpec == Path.GetFullPath(Path.Combine("TestFiles", "three.obj")));
        }

        /// <summary>
        /// Make sure that the compacted read tlog contains the correct information when the composite rooting
        /// markers are kept, as in the case where there is a many-to-one relationship between inputs and
        /// outputs (ie. Lib, Link)
        /// </summary>
        [Fact]
        public void SaveCompactedReadTlog_MaintainCompositeRootingMarkers()
        {
            Console.WriteLine("Test: SaveCompactedReadTlog_MaintainCompositeRootingMarkers");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three2.h"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "three.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "twothree.obj"), "");

            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one1.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one2.read.tlog"), new[] {
                "#Command some-command1",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "two1.read.tlog"), new[] {
                "#Command some-command2",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "three1.read.tlog"), new[] {
                "#Command some-command2",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "three1.h"))
            });

            File.WriteAllLines(Path.Combine("TestFiles", "twothree.read.tlog"), new[] {
                "#Command some-command2",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two3.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "three1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "three2.h"))
            });

            ITaskItem[] tlogs = {
                                    new TaskItem(Path.Combine("TestFiles", "one1.read.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "one2.read.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "two1.read.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "three1.read.tlog")),
                                    new TaskItem(Path.Combine("TestFiles", "twothree.read.tlog"))
                                };

            ITaskItem[] inputs = {
                                     new TaskItem(Path.Combine("TestFiles", "one.cpp")),
                                     new TaskItem(Path.Combine("TestFiles", "two.cpp")),
                                     new TaskItem(Path.Combine("TestFiles", "three.cpp"))
                                 };

            ITaskItem[] outputs = {
                                      new TaskItem(Path.Combine("TestFiles", "one.obj")),
                                      new TaskItem(Path.Combine("TestFiles", "twothree.obj"))
                                  };

            CanonicalTrackedInputFiles d = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    inputs,
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    true /* keep composite rooting markers */
                );

            ITaskItem[] outofdate = d.ComputeSourcesNeedingCompilation();

            // nothing should be out of date
            Assert.Empty(outofdate);
            Assert.Equal(4, d.DependencyTable.Count);

            // dependencies should include the three .h files written into the .tlogs + the rooting marker
            Assert.True(d.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))].Values.Count == 4);

            d.SaveTlog();

            CanonicalTrackedInputFiles d2 = new CanonicalTrackedInputFiles
                (
                    DependencyTestHelper.MockTask,
                    tlogs,
                    inputs,
                    null,
                    outputs,
                    false, /* no minimal rebuild optimization */
                    true /* keep composite rooting markers */
                );

            d2.ComputeSourcesNeedingCompilation();

            Assert.Empty(outofdate);
            Assert.Equal(4, d2.DependencyTable.Count);

            // dependencies should include the three .h files written into the .tlogs + the two rooting marker files
            Assert.True(d2.DependencyTable[Path.GetFullPath(Path.Combine("TestFiles", "three.cpp")) + "|" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp"))].Values.Count == 4);
        }

        [Fact]
        public void InvalidFlatTrackingTLogName()
        {
            Console.WriteLine("Test: InvalidFlatTrackingTLogName");

            // Prepare files
            DependencyTestHelper.WriteAll("TestFiles\\one.h", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.cpp", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.obj", "");
            DependencyTestHelper.WriteAll("TestFiles\\one.tlog", "");

            MockTask task = DependencyTestHelper.MockTask;
            FlatTrackingData data = new FlatTrackingData
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem("TestFiles\\|one|.write.tlog")),
                    false /* don't skip missing files */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(data.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void FlatTrackingTLogWithInitialEmptyLine()
        {
            Console.WriteLine("Test: FlatTrackingTLogWithInitialEmptyLine");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "", "^FOO" });

            MockTask task = DependencyTestHelper.MockTask;
            FlatTrackingData data = new FlatTrackingData
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    false /* don't skip missing files */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(data.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void FlatTrackingTLogWithEmptyLineImmediatelyAfterRoot()
        {
            Console.WriteLine("Test: FlatTrackingTLogWithEmptyLineImmediatelyAfterRoot");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^FOO", "", "FOO" });

            MockTask task = DependencyTestHelper.MockTask;
            FlatTrackingData data = new FlatTrackingData
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    false /* don't skip missing files */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(data.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void FlatTrackingTLogWithEmptyLineBetweenRoots()
        {
            Console.WriteLine("Test: FlatTrackingTLogWithEmptyLineBetweenRoots");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^FOO", "FOO", "", "^BAR", "BAR" });

            MockTask task = DependencyTestHelper.MockTask;
            FlatTrackingData data = new FlatTrackingData
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    false /* don't skip missing files */
                );

            Assert.Equal(1, ((MockEngine) task.BuildEngine).Warnings); // "Should have a warning."
            Assert.Empty(data.DependencyTable); // "DependencyTable should be empty."
        }

        [Fact]
        public void FlatTrackingTLogWithEmptyRoot()
        {
            Console.WriteLine("Test: FlatTrackingTLogWithEmptyRoot");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] { "^", "FOO" });

            MockTask task = DependencyTestHelper.MockTask;
            FlatTrackingData data = new FlatTrackingData
                (
                    task,
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    false /* don't skip missing files */
                );

            Assert.Equal(0, ((MockEngine) task.BuildEngine).Warnings); // "Should not warn -- root markers are ignored by default"
            Assert.Single(data.DependencyTable); // "DependencyTable should only contain one entry."
        }

        [Fact]
        public void FlatTrackingDataMissingInputsAndOutputs()
        {
            Console.WriteLine("Test: FlatTrackingDataMissingInputsAndOutputs");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile.obj")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile2.obj"))
            });

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            Assert.Equal(2, inputs.MissingFiles.Count);
            Assert.Equal(3, outputs.MissingFiles.Count);
        }

        [Fact]
        public void FlatTrackingDataMissingInputs()
        {
            Console.WriteLine("Test: FlatTrackingDataMissingInputs");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "two1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "two2.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
            });

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // No matter which way you look at it, if we're missing inputs, we're out of date
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));
            Assert.Equal(2, inputs.MissingFiles.Count);
            Assert.Empty(outputs.MissingFiles);
        }

        [Fact]
        public void FlatTrackingDataMissingOutputs()
        {
            Console.WriteLine("Test: FlatTrackingDataMissingOutputs");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");
            Thread.Sleep(_sleepTimeMilliseconds); // need to wait since the timestamp check needs some time to register
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                Path.GetFullPath(Path.Combine("TestFiles", "sometempfile2.obj"))
            });

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // No matter which way you look at it, if we're missing outputs, we're out of date
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));
            Assert.Empty(inputs.MissingFiles);
            Assert.Equal(2, outputs.MissingFiles.Count);
        }

        [Fact]
        public void FlatTrackingDataEmptyInputTLogs()
        {
            Console.WriteLine("Test: FlatTrackingDataEmptyInputTLogs");
            // Prepare files
            File.WriteAllText(Path.Combine("TestFiles", "one.read.tlog"), string.Empty);
            File.WriteAllText(Path.Combine("TestFiles", "one.write.tlog"), string.Empty);

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // No matter which way you look at it, if we're missing inputs, we're out of date
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));
        }

        [Fact]
        public void FlatTrackingDataEmptyOutputTLogs()
        {
            Console.WriteLine("Test: FlatTrackingDataEmptyOutputTLogs");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            // Prepare files
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllText(Path.Combine("TestFiles", "one.write.tlog"), string.Empty);

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // Inputs newer than outputs - if there are no outputs, then we're out of date
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            // Inputs newer than tracking - if there are no outputs, then we don't care
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
            // Inputs or Outputs newer than tracking - if there is an output tlog, even if there's no text written to it, we're not out of date
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));
        }

        [Fact]
        public void FlatTrackingDataInputNewerThanTracking()
        {
            Console.WriteLine("Test: FlatTrackingDataInputNewerThanTracking");
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
            });

            Thread.Sleep(_sleepTimeMilliseconds);
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            // Compact the read tlog
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));

            // Touch the tracking logs so that are more recent that any of the inputs
            Thread.Sleep(_sleepTimeMilliseconds);
            File.SetLastWriteTime(Path.Combine("TestFiles", "one.read.tlog"), DateTime.Now);
            File.SetLastWriteTime(Path.Combine("TestFiles", "one.write.tlog"), DateTime.Now);
            Thread.Sleep(_sleepTimeMilliseconds);
            // Touch the output so that we would be out of date with respect to the inputs, but up to date with respect to the tracking logs
            File.SetLastWriteTime(Path.GetFullPath(Path.Combine("TestFiles", "one.obj")), DateTime.Now - TimeSpan.FromHours(1));

            outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // We should be out of date with respect to the outputs
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            // We should be up to date with respect to the tracking data
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
        }

        [Fact]
        public void FlatTrackingDataInputNewerThanTrackingNoOutput()
        {
            Console.WriteLine("Test: FlatTrackingDataInputNewerThanTrackingNoOutput");
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");

            Thread.Sleep(_sleepTimeMilliseconds);

            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "*-one.write.?.tlog"))), false);
            // Compact the read tlog
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            inputs.SaveTlog();
            outputs.SaveTlog();

            outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "*-one.write.?.tlog"))), false);
            // Compact the read tlog
            inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs));
        }

        [Fact]
        public void FlatTrackingDataInputNewerThanOutput()
        {
            Console.WriteLine("Test: FlatTrackingDataInputOrOutputNewerThanTracking");
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
            });
            // Wait so that our tlogs are old
            Thread.Sleep(_sleepTimeMilliseconds);

            // Prepare the source files (later than tracking logs)
            // Therefore newer
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");

            // Prepate the output files (later than tracking logs and source files
            // Therefore newer
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            // Compact the read tlog
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // We should be up to date inputs vs outputs
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));

            // We should be out of date inputs & outputs vs tracking (since we wrote the files after the tracking logs)
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));

            // Touch the input so that we would be out of date with respect to the outputs, and out of date with respect to the tracking logs
            Thread.Sleep(_sleepTimeMilliseconds);
            File.SetLastWriteTime(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")), DateTime.Now);

            outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // We should be out of date with respect to the tracking logs
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanTracking, inputs, outputs), "#3");

            // We should be out of date with respect to the outputs
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs), "#4");
        }

        [Fact]
        public void FlatTrackingDataInputOrOutputNewerThanTracking()
        {
            Console.WriteLine("Test: FlatTrackingDataInputOrOutputNewerThanTracking");
            File.WriteAllLines(Path.Combine("TestFiles", "one.read.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one2.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "one3.h")),
            });

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
            });

            Thread.Sleep(_sleepTimeMilliseconds);
            // Prepare files
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one1.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one2.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one3.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");
            Thread.Sleep(_sleepTimeMilliseconds);
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.obj"), "");

            FlatTrackingData outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            // Compact the read tlog
            FlatTrackingData inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);
            // We should be up to date inputs vs outputs
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
            // We should be out of date inputs & outputs vs tracking (since we wrote the files after the tracking logs)
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));

            // Touch the tracking logs so that are more recent that any of the inputs
            Thread.Sleep(_sleepTimeMilliseconds);
            File.SetLastWriteTime(Path.Combine("TestFiles", "one.read.tlog"), DateTime.Now);
            File.SetLastWriteTime(Path.Combine("TestFiles", "one.write.tlog"), DateTime.Now);
            Thread.Sleep(_sleepTimeMilliseconds);

            outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // We should be up to date with respect to the tracking data
            Assert.True(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputOrOutputNewerThanTracking, inputs, outputs));

            // Touch the input so that we would be out of date with respect to the outputs, but up to date with respect to the tracking logs
            File.SetLastWriteTime(Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")), DateTime.Now);

            outputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))), false);
            inputs = new FlatTrackingData(DependencyTestHelper.MockTask, DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.read.tlog"))), false);

            // We should be out of date with respect to the outputs
            Assert.False(FlatTrackingData.IsUpToDate(DependencyTestHelper.MockTask.Log, UpToDateCheckType.InputNewerThanOutput, inputs, outputs));
        }

        [Fact]
        public void FlatTrackingExcludeDirectories()
        {
            Console.WriteLine("Test: FlatTrackingExcludeDirectories");

            // Prepare files 
            if (!Directory.Exists(Path.Combine("TestFiles", "ToBeExcluded")))
            {
                Directory.CreateDirectory(Path.Combine("TestFiles", "ToBeExcluded"));
            }

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "two.h"), "");

            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.h"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "one.cpp"), "");

            File.WriteAllLines(Path.Combine("TestFiles", "one.tlog"), new[] {
                "#Command some-command",
                Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one1.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "ToBeExcluded", "two.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "ToBeExcluded", "two.h")),
                Path.GetFullPath(Path.Combine("TestFiles", "SubdirectoryExcluded", "three.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "SubdirectoryExcluded", "three.h")),
            });

            // Get the newest time w/o any exclude paths
            Dictionary<string, DateTime> sharedLastWriteTimeUtcCache = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            FlatTrackingData data = new FlatTrackingData
                (
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    null,
                    DateTime.MinValue,
                    null,
                    sharedLastWriteTimeUtcCache
                );

            DateTime originalNewest = data.NewestFileTimeUtc;

            // Force an update to the files we don't care about
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "two.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "two.h"), "");
            if (!Directory.Exists(Path.Combine("TestFiles", "ToBeExcluded", "SubdirectoryExcluded")))
            {
                Directory.CreateDirectory(Path.Combine("TestFiles", "ToBeExcluded", "SubdirectoryExcluded"));
            }
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "SubdirectoryExcluded", "three.cpp"), "");
            DependencyTestHelper.WriteAll(Path.Combine("TestFiles", "ToBeExcluded", "SubdirectoryExcluded", "three.h"), "");

            // Now do a flat tracker ignoring the exclude directories and make sure the time didn't change
            data = new FlatTrackingData
                (
                    DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.tlog"))),
                    null,
                    DateTime.MinValue,
                    new[] { Path.GetFullPath(Path.Combine("TestFiles", "ToBeExcluded")) },
                    sharedLastWriteTimeUtcCache
                );

            Assert.Equal(originalNewest, data.NewestFileTimeUtc); // "Timestamp changed when no tracked files changed."
        }

        [Fact]
        public void TrackingDataCacheResetOnTlogChange()
        {
            Console.WriteLine("Test: FlatTrackingDataCacheResetOnTlogChange");

            File.WriteAllLines(Path.Combine("TestFiles", "one.write.tlog"), new[] {
                "#Command some-command",
                "^" + Path.GetFullPath(Path.Combine("TestFiles", "one.cpp")),
                Path.GetFullPath(Path.Combine("TestFiles", "one.obj")),
            });

            FlatTrackingData outputs = new FlatTrackingData(
                DependencyTestHelper.MockTask,
                DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))),
                false);
            // Sleep once, so that NTFS has enough time to register a file modified time change
            Thread.Sleep(_sleepTimeMilliseconds);
            File.WriteAllLines(
                Path.Combine("TestFiles", "one.write.tlog"),
                new[]
                    {
                        "#Command some-command", "^" + Path.GetFullPath(Path.Combine("TestFiles", "two.cpp")),
                        Path.GetFullPath(Path.Combine("TestFiles", "two.obj")),
                    });

            FlatTrackingData outputs2 = new FlatTrackingData(
                DependencyTestHelper.MockTask,
                DependencyTestHelper.ItemArray(new TaskItem(Path.Combine("TestFiles", "one.write.tlog"))),
                false);

            // We should not use the cached dependency table, since it has been updated since it was last read from disk
            Assert.NotEqual(outputs.DependencyTable, outputs2.DependencyTable);
        }

        [Fact]
        public void RootContainsSubRoots()
        {
            Console.WriteLine("Test: RootContainsSubRoots");
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "a|b|C|d|e|F|g"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "a"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "g"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "d"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "a|b"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "f|g"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "b|a"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "g|f"));
            Assert.True(CanonicalTrackedFilesHelper.RootContainsAllSubRootComponents("a|b|c|d|e|f|g", "b|e"));
        }
    }

    internal class MockTask : Task
    {
        public MockTask(ResourceManager resourceManager)
            : base(resourceManager)
        {
        }

        public TaskLoggingHelper LogHelper => Log;

        public override bool Execute() => true;
    }

    internal class DependencyTestHelper
    {
        public static ITaskItem[] ItemArray(ITaskItem item)
        {
            var itemList = new List<ITaskItem>();
            itemList.Add(item);
            return itemList.ToArray();
        }

        public static MockTask MockTask => new MockTask(AssemblyResources.PrimaryResources) {BuildEngine = new MockEngine()};

        public static void WriteAll(string filename, string content) => File.WriteAllText(filename, content);
    }
}

#endif
