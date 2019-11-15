// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
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
