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
using Microsoft.Build.Construction;
using System.IO;
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
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => null, true);
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
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => ProjectRootElement.Create("c:\\bar"), true);
            }
           );
        }
        /// <summary>
        /// Tests that an entry added to the cache can be retrieved.
        /// </summary>
        [Fact]
        public void AddEntry()
        {
            ProjectRootElement projectRootElement = ProjectRootElement.Create("c:\\foo");
            ProjectRootElement projectRootElement2 = ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => { throw new InvalidOperationException(); }, true);

            Assert.Same(projectRootElement, projectRootElement2);
        }

        /// <summary>
        /// Tests that a strong reference is held to a single item
        /// </summary>
        [Fact]
        public void AddEntryStrongReference()
        {
            ProjectRootElement projectRootElement = ProjectRootElement.Create("c:\\foo");

            projectRootElement = null;
            GC.Collect();

            projectRootElement = ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => { throw new InvalidOperationException(); }, true);

            Assert.NotNull(projectRootElement);

            ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.DiscardStrongReferences();
            projectRootElement = null;
            GC.Collect();

            Assert.Null(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.TryGet("c:\\foo"));
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
                Assert.Equal(true, Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.Equal(false, Object.ReferenceEquals(xml0, xml2));
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
                Assert.Equal(true, Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.Equal(true, Object.ReferenceEquals(xml0, xml2));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
