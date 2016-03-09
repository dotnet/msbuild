// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class RuntimeOutputFiles : CompilationOutputFiles
    {
        public RuntimeOutputFiles(string basePath,
            Project project,
            string configuration,
            NuGetFramework framework) : base(basePath, project, configuration, framework)
        {
        }

        public string Executable
        {
            get
            {
                var extension = FileNameSuffixes.CurrentPlatform.Exe;

                // This is the check for mono, if we're not on windows and producing outputs for
                // the desktop framework then it's an exe
                if (Framework.IsDesktop())
                {
                    extension = FileNameSuffixes.DotNet.Exe;
                }
                return Path.Combine(BasePath, Project.Name + extension);
            }
        }

        public string Deps
        {
            get
            {
                return Path.ChangeExtension(Assembly, FileNameSuffixes.Deps);
            }
        }
        public string DepsJson
        {
            get
            {
                return Path.ChangeExtension(Assembly, FileNameSuffixes.DepsJson);
            }
        }

        public string Config
        {
            get { return Assembly + ".config"; }
        }

        public override IEnumerable<string> All()
        {
            foreach (var file in base.All())
            {
                yield return file;
            }

            if (File.Exists(Config))
            {
                yield return Config;
            }

            if (File.Exists(Deps))
            {
                yield return Deps;
            }

            if (File.Exists(DepsJson))
            {
                yield return DepsJson;
            }
        }
    }
}