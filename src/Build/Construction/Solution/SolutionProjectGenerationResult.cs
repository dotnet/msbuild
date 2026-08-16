// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Execution;

namespace Microsoft.Build.Construction
{
    // This removes the implicit contract that element zero of an array is the traversal project.
    internal readonly struct SolutionProjectGenerationResult
    {
        internal SolutionProjectGenerationResult(ProjectInstance traversalProject, IReadOnlyList<ProjectInstance> metaprojects)
        {
            TraversalProject = traversalProject;
            Metaprojects = metaprojects;
        }

        internal ProjectInstance TraversalProject { get; }

        internal IReadOnlyList<ProjectInstance> Metaprojects { get; }

        internal ProjectInstance[] ToProjectInstances()
        {
            var instances = new ProjectInstance[Metaprojects.Count + 1];
            instances[0] = TraversalProject;

            for (int i = 0; i < Metaprojects.Count; i++)
            {
                instances[i + 1] = Metaprojects[i];
            }

            return instances;
        }
    }
}