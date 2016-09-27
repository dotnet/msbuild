using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectLibraryDependency : LibraryDependency
    {
        public string SourceFilePath { get; set;  }
        public int SourceLine { get; set; }
        public int SourceColumn { get; set; }

        public ProjectLibraryDependency()
        {
        }

        public ProjectLibraryDependency(LibraryRange libraryRange)
        {
            LibraryRange = libraryRange;
        }
    }
}