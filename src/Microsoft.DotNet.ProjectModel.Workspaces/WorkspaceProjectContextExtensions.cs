using Microsoft.CodeAnalysis;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Workspaces
{
    public static class WorkspaceProjectContextExtensions
    {
        public static Workspace CreateWorkspace(this ProjectContext context)
        {
            return new ProjectJsonWorkspace(context);
        }
    }
}