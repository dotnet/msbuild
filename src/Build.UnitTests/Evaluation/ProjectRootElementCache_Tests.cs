// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;



#nullable disable

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    /// <summary>
    /// Tests for ProjectRootElementCache
    /// </summary>
    public class ProjectRootElementCache_Tests : IDisposable
    {
        /// <summary>
        /// Set up the test
        /// </summary>
        public ProjectRootElementCache_Tests()
        {
            // Empty the cache
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Tear down the test
        /// </summary>
        public void Dispose()
        {
            // Empty the cache
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Verifies that a null entry fails
        /// </summary>
        [Fact]
        public void AddNull()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => null, true, false);
            });
        }
        /// <summary>
        /// Verifies that the delegate cannot return a project with a different path
        /// </summary>
        [Fact]
        public void AddUnsavedProject()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => ProjectRootElement.Create("c:\\bar"), true, false);
            });
        }
        /// <summary>
        /// Tests that an entry added to the cache can be retrieved.
        /// </summary>
        [Fact]
        public void AddEntry()
        {
            string rootedPath = NativeMethodsShared.IsUnixLike ? "/foo" : "c:\\foo";
            ProjectRootElement projectRootElement = ProjectRootElement.Create(rootedPath);
            ProjectRootElement projectRootElement2 = ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get(rootedPath, (p, c) => { throw new InvalidOperationException(); }, true, false);

            Assert.Same(projectRootElement, projectRootElement2);
        }

        /// <summary>
        /// Tests that a strong reference is held to a single item
        /// </summary>
        [WindowsFullFrameworkOnlyFact(additionalMessage: "This test fails on .NET Core: https://github.com/dotnet/msbuild/issues/282")]
        public void AddEntryStrongReference()
        {
            string projectPath = NativeMethodsShared.IsUnixLike ? "/foo" : "c:\\foo";
            ProjectRootElement projectRootElement = ProjectRootElement.Create(projectPath);

            projectRootElement = null;
            GC.Collect();

            projectRootElement = ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get(projectPath, (p, c) => { throw new InvalidOperationException(); }, true, false);

            Assert.NotNull(projectRootElement);

            ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.DiscardStrongReferences();
            projectRootElement = null;
            GC.Collect();

            Assert.Null(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.TryGet(projectPath));
        }

        /// <summary>
        /// Cache should not return a ProjectRootElement if the file it was loaded from has since changed -
        /// if the cache was configured to auto-reload.
        /// </summary>
        [Fact]
        public void GetProjectRootElementChangedOnDisk1()
        {
            string path = null;

            try
            {
                ProjectRootElementCache cache = new ProjectRootElementCache(true /* auto reload from disk */);

                path = FileUtilities.GetTemporaryFileName();

                ProjectRootElement xml0 = ProjectRootElement.Create(path);
                xml0.Save();

                cache.AddEntry(xml0);

                ProjectRootElement xml1 = cache.TryGet(path);
                Assert.True(Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.False(Object.ReferenceEquals(xml0, xml2));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Cache should return a ProjectRootElement directly even if the file it was loaded from has since changed -
        /// if the cache was configured to NOT auto-reload.
        /// </summary>
        [Fact]
        public void GetProjectRootElementChangedOnDisk2()
        {
            string path = null;

            try
            {
                ProjectRootElementCache cache = new ProjectRootElementCache(false /* do not auto reload from disk */);

                path = FileUtilities.GetTemporaryFileName();

                ProjectRootElement xml0 = ProjectRootElement.Create(path);
                xml0.Save();

                cache.AddEntry(xml0);

                ProjectRootElement xml1 = cache.TryGet(path);
                Assert.True(Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.True(Object.ReferenceEquals(xml0, xml2));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies that concurrent lookups from multiple threads do not corrupt the cache.
        /// This stress tests the lock-free weak cache lookup and the non-blocking strong cache boost.
        /// </summary>
        [Fact]
        public void ConcurrentGetFromMultipleThreads()
        {
            const int threadCount = 8;
            const int iterationsPerThread = 200;

            ProjectRootElementCache cache = new ProjectRootElementCache(false);

            // Pre-populate cache with entries.
            ProjectRootElement[] elements = new ProjectRootElement[20];
            for (int i = 0; i < elements.Length; i++)
            {
                string path = NativeMethodsShared.IsUnixLike ? $"/concurrent_test_{i}.proj" : $"c:\\concurrent_test_{i}.proj";
                elements[i] = ProjectRootElement.Create(path);
                cache.AddEntry(elements[i]);
            }

            Exception caughtException = null;
            using System.Threading.CountdownEvent countdown = new(threadCount);
            using System.Threading.ManualResetEventSlim startEvent = new(false);

            for (int t = 0; t < threadCount; t++)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        startEvent.Wait();
                        Random random = new Random(System.Threading.Thread.CurrentThread.ManagedThreadId);

                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            int idx = random.Next(elements.Length);
                            string path = elements[idx].FullPath;

                            // Lookup should return the same PRE we added.
                            ProjectRootElement result = cache.TryGet(path);
                            if (result != null && !ReferenceEquals(result, elements[idx]))
                            {
                                System.Threading.Interlocked.CompareExchange(
                                    ref caughtException,
                                    new Exception($"Cache returned wrong PRE for {path}"),
                                    null);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.CompareExchange(ref caughtException, ex, null);
                    }
                    finally
                    {
                        countdown.Signal();
                    }
                });
            }

            // Release all threads at once.
            startEvent.Set();
            countdown.Wait(TimeSpan.FromSeconds(30));

            Assert.Null(caughtException);
        }

        /// <summary>
        /// Verifies that concurrent AddEntry and TryGet calls do not corrupt the cache.
        /// </summary>
        [Fact]
        public void ConcurrentAddAndGet()
        {
            const int threadCount = 8;
            const int entriesPerThread = 50;

            ProjectRootElementCache cache = new ProjectRootElementCache(false);
            Exception caughtException = null;
            using System.Threading.CountdownEvent countdown = new(threadCount);
            using System.Threading.ManualResetEventSlim startEvent = new(false);

            // Each thread creates and adds its own entries, while also looking up others.
            ProjectRootElement[][] allElements = new ProjectRootElement[threadCount][];
            for (int t = 0; t < threadCount; t++)
            {
                allElements[t] = new ProjectRootElement[entriesPerThread];
                for (int i = 0; i < entriesPerThread; i++)
                {
                    string path = NativeMethodsShared.IsUnixLike
                        ? $"/addget_t{t}_{i}.proj"
                        : $"c:\\addget_t{t}_{i}.proj";
                    allElements[t][i] = ProjectRootElement.Create(path);
                }
            }

            for (int t = 0; t < threadCount; t++)
            {
                int threadIdx = t;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        startEvent.Wait();

                        for (int i = 0; i < entriesPerThread; i++)
                        {
                            // Add our entry.
                            cache.AddEntry(allElements[threadIdx][i]);

                            // Try to get a random entry from any thread.
                            Random random = new Random(threadIdx * 1000 + i);
                            int otherThread = random.Next(threadCount);
                            int otherEntry = random.Next(entriesPerThread);
                            ProjectRootElement other = allElements[otherThread][otherEntry];

                            ProjectRootElement result = cache.TryGet(other.FullPath);

                            // Result might be null (not yet added) or the correct entry.
                            if (result != null && !ReferenceEquals(result, other))
                            {
                                System.Threading.Interlocked.CompareExchange(
                                    ref caughtException,
                                    new Exception($"Cache returned wrong PRE for {other.FullPath}"),
                                    null);
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.CompareExchange(ref caughtException, ex, null);
                    }
                    finally
                    {
                        countdown.Signal();
                    }
                });
            }

            startEvent.Set();
            countdown.Wait(TimeSpan.FromSeconds(30));

            Assert.Null(caughtException);
        }

        /// <summary>
        /// Verifies that concurrent AddEntry, TryGet, and ForgetEntry (via DiscardAnyWeakReference)
        /// do not deadlock or corrupt the cache.
        /// </summary>
        [Fact]
        public void ConcurrentAddGetAndDiscard()
        {
            const int threadCount = 6;
            const int iterations = 100;

            ProjectRootElementCache cache = new ProjectRootElementCache(false);
            Exception caughtException = null;
            using System.Threading.CountdownEvent countdown = new(threadCount);
            using System.Threading.ManualResetEventSlim startEvent = new(false);

            // Create a pool of elements shared across threads.
            ProjectRootElement[] pool = new ProjectRootElement[30];
            for (int i = 0; i < pool.Length; i++)
            {
                string path = NativeMethodsShared.IsUnixLike
                    ? $"/discard_test_{i}.proj"
                    : $"c:\\discard_test_{i}.proj";
                pool[i] = ProjectRootElement.Create(path);
            }

            for (int t = 0; t < threadCount; t++)
            {
                int threadIdx = t;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        startEvent.Wait();
                        Random random = new Random(threadIdx);

                        for (int i = 0; i < iterations; i++)
                        {
                            int idx = random.Next(pool.Length);
                            int op = random.Next(3);

                            switch (op)
                            {
                                case 0:
                                    cache.AddEntry(pool[idx]);
                                    break;
                                case 1:
                                    cache.TryGet(pool[idx].FullPath);
                                    break;
                                case 2:
                                    cache.DiscardAnyWeakReference(pool[idx]);
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Threading.Interlocked.CompareExchange(ref caughtException, ex, null);
                    }
                    finally
                    {
                        countdown.Signal();
                    }
                });
            }

            startEvent.Set();
            countdown.Wait(TimeSpan.FromSeconds(30));

            Assert.Null(caughtException);
        }
    }
}
