// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation
{
    internal abstract class ProjectRootElementCacheBase
    {
        public bool LoadProjectsReadOnly { get; protected set; }

        /// <summary>
        /// Handler for which project root element just got added to the cache
        /// </summary>
        internal delegate void ProjectRootElementCacheAddEntryHandler(object sender, ProjectRootElementCacheAddEntryEventArgs e);

        /// <summary>
        /// Delegate for StrongCacheEntryRemoved event
        /// </summary>
        internal delegate void StrongCacheEntryRemovedDelegate(object sender, ProjectRootElement projectRootElement);

        /// <summary>
        /// Callback to create a ProjectRootElement if need be
        /// </summary>
        internal delegate ProjectRootElement OpenProjectRootElement(string path, ProjectRootElementCacheBase cache);

        /// <summary>
        /// Event that is fired when an entry in the Strong Cache is removed.
        /// </summary>
        internal static event StrongCacheEntryRemovedDelegate StrongCacheEntryRemoved;

        /// <summary>
        /// Event which is fired when a project root element is added to this cache.
        /// </summary>
        internal event ProjectRootElementCacheAddEntryHandler ProjectRootElementAddedHandler;

        /// <summary>
        /// Event which is fired when a project root element in this cache is dirtied.
        /// </summary>
        internal event EventHandler<ProjectXmlChangedEventArgs> ProjectRootElementDirtied;

        /// <summary>
        /// Event which is fired when a project is marked dirty.
        /// </summary>
        internal event EventHandler<ProjectChangedEventArgs> ProjectDirtied;

        internal abstract ProjectRootElement Get(string projectFile, OpenProjectRootElement openProjectRootElement,
            bool isExplicitlyLoaded,
            bool? preserveFormatting);

        internal abstract void AddEntry(ProjectRootElement projectRootElement);

        internal abstract void RenameEntry(string oldFullPath, ProjectRootElement projectRootElement);

        internal abstract ProjectRootElement TryGet(string projectFile);

        internal abstract ProjectRootElement TryGet(string projectFile, bool? preserveFormatting);

        internal abstract void DiscardStrongReferences();

        internal abstract void Clear();

        internal abstract void DiscardImplicitReferences();

        internal abstract void DiscardAnyWeakReference(ProjectRootElement projectRootElement);

        /// <summary>
        /// Raises the <see cref="ProjectRootElementDirtied"/> event.
        /// </summary>
        /// <param name="sender">The dirtied project root element.</param>
        /// <param name="e">Details on the PRE and the nature of the change.</param>
        internal virtual void OnProjectRootElementDirtied(ProjectRootElement sender, ProjectXmlChangedEventArgs e)
        {
            var cacheDirtied = ProjectRootElementDirtied;
            cacheDirtied?.Invoke(sender, e);
        }

        /// <summary>
        /// Raises the <see cref="ProjectDirtied"/> event.
        /// </summary>
        /// <param name="sender">The dirtied project.</param>
        /// <param name="e">Details on the Project and the change.</param>
        internal virtual void OnProjectDirtied(Project sender, ProjectChangedEventArgs e)
        {
            var projectDirtied = ProjectDirtied;
            projectDirtied?.Invoke(sender, e);
        }

        /// <summary>
        /// Raises an event which is raised when a project root element is added to the cache.
        /// </summary>
        protected void RaiseProjectRootElementAddedToCacheEvent(ProjectRootElement rootElement)
        {
            ProjectRootElementAddedHandler?.Invoke(this, new ProjectRootElementCacheAddEntryEventArgs(rootElement));
        }

        /// <summary>
        /// Raises an event which is raised when a project root element is removed from the strong cache.
        /// </summary>
        protected virtual void RaiseProjectRootElementRemovedFromStrongCache(ProjectRootElement projectRootElement)
        {
            StrongCacheEntryRemovedDelegate removedEvent = StrongCacheEntryRemoved;
            removedEvent?.Invoke(this, projectRootElement);
        }
    }

    /// <summary>
    /// This class is an event that holds which ProjectRootElement was added to the root element cache.
    /// </summary>
    internal class ProjectRootElementCacheAddEntryEventArgs : EventArgs
    {
        /// <summary>
        /// Takes the root element which was added to the results cache.
        /// </summary>
        internal ProjectRootElementCacheAddEntryEventArgs(ProjectRootElement element)
        {
            RootElement = element;
        }

        /// <summary>
        /// Root element which was just added to the cache.
        /// </summary>
        internal readonly ProjectRootElement RootElement;
    }
}
