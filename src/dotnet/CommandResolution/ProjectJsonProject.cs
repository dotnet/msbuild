using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandResolution
{
    internal class ProjectJsonProject : IProject
    {
        private LockFile _lockFile;

        public ProjectJsonProject(string projectDirectory)
        {
            var lockFilePath = Path.Combine(projectDirectory, LockFileFormat.LockFileName);
            _lockFile = new LockFileFormat().Read(lockFilePath);
        }

        public LockFile GetLockFile()
        {
            return _lockFile;
        }

        public IEnumerable<SingleProjectInfo> GetTools()
        {
            var tools = _lockFile.Tools.Where(t => t.Name.Contains(".NETCoreApp")).SelectMany(t => t.Libraries);

            return tools.Select(t => new SingleProjectInfo(
                t.Name,
                t.Version.ToFullString(),
                Enumerable.Empty<ResourceAssemblyInfo>()));
        }
    }
}