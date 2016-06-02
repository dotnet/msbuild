// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ProjectModel
{
    public class BuildWorkspace : Workspace
    {
        public BuildWorkspace(ProjectReaderSettings settings) : base(settings, false) { }

        /// <summary>
        /// Create an empty <see cref="WorkspaceContext" /> using the default <see cref="ProjectReaderSettings" />
        /// </summary>
        /// <returns></returns>
        public static BuildWorkspace Create() => Create(versionSuffix: string.Empty);

        /// <summary>
        /// Create an empty <see cref="WorkspaceContext" /> using the default <see cref="ProjectReaderSettings" />, with the specified Version Suffix
        /// </summary>
        /// <param name="versionSuffix">The suffix to use to replace any '-*' snapshot tokens in Project versions.</param>
        /// <returns></returns>
        public static BuildWorkspace Create(string versionSuffix)
        {
            var settings = ProjectReaderSettings.ReadFromEnvironment();
            if (!string.IsNullOrEmpty(versionSuffix))
            {
                settings.VersionSuffix = versionSuffix;
            }
            return new BuildWorkspace(settings);
        }

        public ProjectContext GetRuntimeContext(ProjectContext context, IEnumerable<string> runtimeIdentifiers)
        {
            if (!runtimeIdentifiers.Any())
            {
                return context;
            }

            var contexts = GetProjectContextCollection(context.ProjectDirectory);
            if (contexts == null)
            {
                return null;
            }

            var runtimeContext = runtimeIdentifiers
                .Select(r => contexts.GetTarget(context.TargetFramework, r))
                .FirstOrDefault(c => c != null);

            if (runtimeContext == null)
            {
                if (context.IsPortable)
                {
                    // We're specializing a portable target, so synthesize a runtime target manually
                    // We don't cache this project context, but we'll still use the cached Project and LockFile
                    return CreateBaseProjectBuilder(context.ProjectFile)
                        .WithTargetFramework(context.TargetFramework)
                        .WithRuntimeIdentifiers(runtimeIdentifiers)
                        .Build();
                }

                // We are standalone, but don't support this runtime
                var rids = string.Join(", ", runtimeIdentifiers);
                throw new InvalidOperationException($"Can not find runtime target for framework '{context.TargetFramework}' compatible with one of the target runtimes: '{rids}'. " +
                                                    "Possible causes:" + Environment.NewLine +
                                                    "1. The project has not been restored or restore failed - run `dotnet restore`" + Environment.NewLine +
                                                    $"2. The project does not list one of '{rids}' in the 'runtimes' section." + Environment.NewLine +
                                                    "3. You may be trying to publish a library, which is not supported. Use `dotnet pack` to distribute libraries.");
            }

            return runtimeContext;
        }

        protected override IEnumerable<ProjectContext> BuildProjectContexts(Project project)
        {
            return CreateBaseProjectBuilder(project).BuildAllTargets();
        }
    }
}
