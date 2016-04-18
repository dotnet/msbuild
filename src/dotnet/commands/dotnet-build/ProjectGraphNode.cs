using Microsoft.DotNet.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Build
{
    public class ProjectGraphNode
    {
        private readonly Task<ProjectContext> _projectContextCreator;

        public ProjectGraphNode(Task<ProjectContext> projectContext, IEnumerable<ProjectGraphNode> dependencies, bool isRoot = false)
        {
            _projectContextCreator = projectContext;
            Dependencies = dependencies.ToList();
            IsRoot = isRoot;
        }

        public ProjectContext ProjectContext { get { return _projectContextCreator.GetAwaiter().GetResult(); } }

        public IReadOnlyList<ProjectGraphNode> Dependencies { get; }

        public bool IsRoot { get; }
    }
}