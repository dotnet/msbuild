// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using System;
using System.IO;

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
        [Fact]
        //  This test fails on .NET Core and Mono: https://github.com/Microsoft/msbuild/issues/282
        [Trait("Category", "non-mono-tests")]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "https://github.com/Microsoft/msbuild/issues/282")]
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

                path = FileUtilities.GetTemporaryFile();

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
    }
}
