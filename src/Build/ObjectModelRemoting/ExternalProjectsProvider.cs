// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// Enable providing access to external [potentially remote] ProjectCollection.
    /// </summary>
    public abstract class ExternalProjectsProvider
    {
        /// <summary>
        /// Provide the list of remote projects (projects in the remote collection)
        /// Note all returned objects will be local "linked" Project object proxies.
        /// </summary>
        /// <param name="filePath">[optional] project full path. Can be null in which case function will return all projects</param>
        public abstract ICollection<Project> GetLoadedProjects(string filePath);

        /// <summary>
        /// Called when External provider is "disconnected" from the local collection - aka it will be no longer used to extend
        /// the projects list.
        /// This is triggered by either project collection disposing or when another call to SetExternalProjectsProvider is invoked.
        /// The purpose of this call is to allow the external provider release any associate data (caches/connections etc).
        /// </summary>
        public virtual void Disconnected(ProjectCollection collection) { }

        /// <summary>
        /// Attach an external project provider to a msbuild ProjectCollection.
        ///
        /// Note at any time there could be only one ExternalProvider attached.
        ///
        /// Can be called with link == null, in which case it will "clear" the external provider on the target collection
        /// </summary>
        public static void SetExternalProjectsProvider(ProjectCollection collection, ExternalProjectsProvider link)
        {
            collection.Link = link;
        }
    }
}
