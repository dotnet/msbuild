// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// ProjectCollectionRootElementCache represents the cache used by a <see cref="ProjectCollection"/> for <see cref="ProjectRootElement"/>.
/// </summary>
public class ProjectCollectionRootElementCache
{
    internal readonly ProjectRootElementCacheBase ProjectRootElementCache;

    /// <summary>
    /// Initialize a ProjectCollectionRootElementCache instance.
    /// </summary>
    /// <param name="loadProjectsReadOnly">If set to true, load all projects as read-only.</param>
    /// <param name="autoReloadFromDisk">If set to true, Whether the cache should check the timestamp of the file on disk whenever it is requested, and update with the latest content of that file if it has changed.</param>
    public ProjectCollectionRootElementCache(bool loadProjectsReadOnly, bool autoReloadFromDisk = false)
    {
        if (Traits.Instance.UseSimpleProjectRootElementCacheConcurrency)
        {
            ProjectRootElementCache = new SimpleProjectRootElementCache();
        }
        else
        {
            ProjectRootElementCache = new ProjectRootElementCache(autoReloadFromDisk: autoReloadFromDisk, loadProjectsReadOnly);
        }
    }
}
