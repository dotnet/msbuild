using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Internal.ProjectModel.Graph
{
    internal class ExportFile
    {
        public static readonly string ExportFileName = "project.fragment.lock.json";

        public int Version { get; }
        public string ExportFilePath { get; }

        public IList<LockFileTargetLibrary> Exports { get; }

        public ExportFile(string exportFilePath, int version, IList<LockFileTargetLibrary> exports)
        {
            ExportFilePath = exportFilePath;
            Version = version;
            Exports = exports.Any() ? exports : new List<LockFileTargetLibrary>(0);
        }
    }
}