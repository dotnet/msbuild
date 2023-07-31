// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Msbuild.Tests.Utilities
{
    public class ProjDir
    {
        public ProjDir(string path)
        {
            Path = path;
            Name = new DirectoryInfo(Path).Name;
        }

        public string Path { get; private set; }
        public string Name { get; private set; }
        public string CsProjName => $"{Name}.csproj";
        public string CsProjPath => System.IO.Path.Combine(Path, CsProjName);

        public string CsProjContent()
        {
            return File.ReadAllText(CsProjPath);
        }

        public ProjectRootElement CsProj()
        {
            // Passing new collection to prevent using cached version
            return ProjectRootElement.Open(CsProjPath, new ProjectCollection());
        }
    }
}
