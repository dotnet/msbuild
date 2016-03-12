// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class RuntimeOutputFiles : CompilationOutputFiles
    {
        private readonly string _runtimeIdentifier;

        public RuntimeOutputFiles(string basePath,
            Project project,
            string configuration,
            NuGetFramework framework,
            string runtimeIdentifier) : base(basePath, project, configuration, framework)
        {
            _runtimeIdentifier = runtimeIdentifier;
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

        public string RuntimeConfigJson
        {
            get
            {
                return Path.ChangeExtension(Assembly, FileNameSuffixes.RuntimeConfigJson);
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

            if (Project.HasRuntimeOutput(Config))
            {
                if (!Framework.IsDesktop())
                {
                    yield return Deps;
                    yield return DepsJson;
                    yield return RuntimeConfigJson;
                }

                // If the project actually has an entry point AND we're doing a standalone build
                var hasEntryPoint = Project.GetCompilerOptions(targetFramework: null, configurationName: Configuration).EmitEntryPoint ?? false;
                if (hasEntryPoint && !string.IsNullOrEmpty(_runtimeIdentifier))
                {
                    // Yield the executable
                    yield return Executable;
                }
            }

            if (File.Exists(Config))
            {
                yield return Config;
            }
        }
    }
}
