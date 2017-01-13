// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for ProjectRootElementCache</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using System.Collections;
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using Xunit;



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
            }
           );
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
            }
           );
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
#if MONO
        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/282")]
#else
        [Fact]
#endif

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

                path = FileUtilities.GetTemporaryFile();

                ProjectRootElement xml0 = ProjectRootElement.Create(path);
                xml0.Save();
                var version0 = xml0.Version;

                cache.AddEntry(xml0);

                ProjectRootElement xml1 = cache.TryGet(path);
                var version1 = xml1.Version;
                Assert.Same(xml0, xml1);
                Assert.Equal(version0, version1);

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                var version2 = xml2.Version;
                Assert.Same(xml0, xml2);
                Assert.NotEqual(version0, version2);
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

                path = FileUtilities.GetTemporaryFile();

                ProjectRootElement xml0 = ProjectRootElement.Create(path);
                xml0.Save();
                var version0 = xml0.Version;

                cache.AddEntry(xml0);

                ProjectRootElement xml1 = cache.TryGet(path);
                var version1 = xml1.Version;
                Assert.Same(xml0, xml1);
                Assert.Equal(version0, version1);

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                var version2 = xml2.Version;
                Assert.Same(xml0, xml1);
                Assert.Equal(version0, version2);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// This test replicates the scenario in https://devdiv.visualstudio.com/DevDiv/_workitems?id=366077&_a=edit
        /// Two different caches can interfere with each other when auto reloading from disk is turned on.
        /// </summary>
        [Fact]
        public void GetProjectRootElementChangedOnDisk3()
        {
            string path = null;

            try
            {
                var nonReloadingCache = new ProjectRootElementCache(autoReloadFromDisk: false);
                var reloadingCache = new ProjectRootElementCache(autoReloadFromDisk: true);

                path = FileUtilities.GetTemporaryFile();

                var nonReloadingProject = ProjectRootElement.Create(path);
                nonReloadingCache.AddEntry(nonReloadingProject);

                nonReloadingProject.Save();

                var previousReloadingProject = ProjectRootElement.Open(path, reloadingCache, true, null);
                Assert.NotSame(nonReloadingProject, previousReloadingProject);

                for (int i = 0; i < 30; i++)
                {
                    var newItemType = $"i_{i}";
                    var newItemValue = $"{i}";

                    nonReloadingProject = ProjectRootElement.Open(path, nonReloadingCache, true, null);
                    nonReloadingProject.AddItem(newItemType, newItemValue);
                    nonReloadingProject.Save();
                    Assert.False(nonReloadingProject.HasUnsavedChanges);

                    DateTime lastWrite = new FileInfo(path).LastWriteTimeUtc;
                    File.SetLastWriteTime(path, lastWrite + new TimeSpan(i + 1, 0, 0));

                    var reloadingProject = ProjectRootElement.Open(path, reloadingCache, true, null);
                    Assert.False(reloadingProject.HasUnsavedChanges);
                    Assert.Same(previousReloadingProject, reloadingProject);

                    Assert.Equal(i + 1, reloadingProject.Items.Count);

                    var lastItemElement = reloadingProject.Items.Last();
                    Assert.Equal(newItemType, lastItemElement.ItemType);
                    Assert.Equal(newItemValue, lastItemElement.Include);

                    previousReloadingProject = reloadingProject;
                }
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
