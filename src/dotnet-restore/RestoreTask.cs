using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Restore
{
    public struct RestoreTask
    {
        public string ProjectPath { get; set; }

        public IEnumerable<string> Arguments { get; set; }

        public string ProjectDirectory => ProjectPath.EndsWith(Project.FileName, StringComparison.OrdinalIgnoreCase) 
            ? Path.GetDirectoryName(ProjectPath) 
            : ProjectPath;
    }
}