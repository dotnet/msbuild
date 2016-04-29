// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Build
{
    internal class ProjectGraphNode
    {
        private readonly Task<ProjectContext> _projectContextCreator;

        public ProjectGraphNode(Task<ProjectContext> projectContext, IEnumerable<ProjectGraphNode> dependencies, bool isRoot = false)
        {
            _projectContextCreator = projectContext;
            Dependencies = dependencies.ToList();
            IsRoot = isRoot;
        }

        public ProjectContext ProjectContext
        {
            get
            {
                using (_projectContextCreator.IsCompleted ? null : PerfTrace.Current.CaptureTiming("", "Blocking ProjectContext wait"))
                {
                    return _projectContextCreator.GetAwaiter().GetResult();
                }
            }
        }

        public IReadOnlyList<ProjectGraphNode> Dependencies { get; }

        public bool IsRoot { get; }
    }
}