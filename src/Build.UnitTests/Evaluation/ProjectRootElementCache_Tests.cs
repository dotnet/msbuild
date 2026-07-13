// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;



#nullable disable

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    /// <summary>
    /// Tests for ProjectRootElementCache
    /// </summary>
    [TestClass]
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
        [MSBuildTestMethod]
        public void AddNull()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => null, true, false);
            });
        }
        /// <summary>
        /// Verifies that the delegate cannot return a project with a different path
        /// </summary>
        [MSBuildTestMethod]
        public void AddUnsavedProject()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get("c:\\foo", (p, c) => ProjectRootElement.Create("c:\\bar"), true, false);
            });
        }
        /// <summary>
        /// Tests that an entry added to the cache can be retrieved.
        /// </summary>
        [MSBuildTestMethod]
        public void AddEntry()
        {
            string rootedPath = NativeMethodsShared.IsUnixLike ? "/foo" : "c:\\foo";
            ProjectRootElement projectRootElement = ProjectRootElement.Create(rootedPath);
            ProjectRootElement projectRootElement2 = ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.Get(rootedPath, (p, c) => { throw new InvalidOperationException(); }, true, false);

            Assert.AreSame(projectRootElement, projectRootElement2);
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

            Assert.IsNotNull(projectRootElement);

            ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.DiscardStrongReferences();
            projectRootElement = null;
            GC.Collect();

            Assert.IsNull(ProjectCollection.GlobalProjectCollection.ProjectRootElementCache.TryGet(projectPath));
        }

        /// <summary>
        /// Cache should not return a ProjectRootElement if the file it was loaded from has since changed -
        /// if the cache was configured to auto-reload.
        /// </summary>
        [MSBuildTestMethod]
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
                Assert.IsTrue(Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.IsFalse(Object.ReferenceEquals(xml0, xml2));
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
        [MSBuildTestMethod]
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
                Assert.IsTrue(Object.ReferenceEquals(xml0, xml1));

                File.SetLastWriteTime(path, DateTime.Now + new TimeSpan(1, 0, 0));

                ProjectRootElement xml2 = cache.TryGet(path);
                Assert.IsTrue(Object.ReferenceEquals(xml0, xml2));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
