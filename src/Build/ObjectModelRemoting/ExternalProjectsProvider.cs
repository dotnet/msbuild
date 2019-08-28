// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.ObjectModelRemoting
{
    using System.Collections.Generic;
    using Microsoft.Build.Evaluation;

    /// <summary>
    /// Enable providing access to external [potentially remote] ProjectCollection.
    /// </summary>
    public abstract class ExternalProjectsProvider
    {
        public abstract ICollection<Project> GetLoadedProjects(string filePath);

        public virtual void Disconnected(ProjectCollection collection) { }

        public static void SetExternalProjectsProvider(ProjectCollection collection, ExternalProjectsProvider link)
        {
            collection.Link = link;
        }
    }
}
