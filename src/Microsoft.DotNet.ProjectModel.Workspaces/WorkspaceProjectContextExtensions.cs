using RoslynWorkspace = Microsoft.CodeAnalysis.Workspace;

namespace Microsoft.DotNet.ProjectModel.Workspaces
{
    public static class WorkspaceProjectContextExtensions
    {
        public static RoslynWorkspace CreateRoslynWorkspace(this ProjectContext context) => new ProjectJsonWorkspace(context);
    }
}